using System.IO.Compression;
using System.Xml;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Source;

namespace Mustask.Infrastructure.Imports.Reading;

/// <summary>
/// The file-safety refusals of a .xlsx, applied to the zip container <b>before the workbook is opened</b> and before any cell is read
/// (doc-017 NFR file safety, BR-IE-21): too many entries, a total expansion past 100 MB (counted on the bytes actually produced by the
/// decompressor, never on the sizes the headers claim), a macro, a reference to another file. Only the parts it must inspect are parsed
/// (content types and relationships), with DTDs prohibited and no resolver.
/// </summary>
internal static class ZipSafetyGate
{
    private const int MaxInspectedPartBytes = 5 * 1024 * 1024;

    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    private static readonly byte[] OleSignature = [0xD0, 0xCF, 0x11, 0xE0];

    /// <summary>Checks the container; throws <see cref="SourceRefusal"/>'s refusal for anything unsafe.</summary>
    public static void Check(byte[] content)
    {
        if (StartsWith(content, OleSignature))
        {
            // A binary .xls renamed, or an encrypted workbook: neither is a zip we can read.
            throw SourceRefusal.Because(ImportErrorCodes.FileUnsupportedFormat);
        }

        if (!StartsWith(content, ZipSignature))
        {
            throw SourceRefusal.Because(ImportErrorCodes.FileUnreadable);
        }

        ZipArchive archive;

        try
        {
            archive = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
        }
        catch (InvalidDataException exception)
        {
            throw SourceRefusal.Because(ImportErrorCodes.FileUnreadable, inner: exception);
        }

        using (archive)
        {
            try
            {
                Inspect(archive);
            }
            catch (InvalidDataException exception)
            {
                throw SourceRefusal.Because(ImportErrorCodes.FileUnreadable, inner: exception);
            }
            catch (XmlException exception)
            {
                throw SourceRefusal.Because(ImportErrorCodes.FileUnreadable, inner: exception);
            }
        }
    }

    private static void Inspect(ZipArchive archive)
    {
        if (archive.Entries.Count > ImportLimits.MaxZipEntries)
        {
            throw SourceRefusal.Because(ImportErrorCodes.FileExpandsTooMuch);
        }

        long declared = 0;
        var hasContentTypes = false;

        foreach (var entry in archive.Entries)
        {
            var name = Normalize(entry.FullName);

            if (name.EndsWith("vbaproject.bin", StringComparison.Ordinal) || name.StartsWith("xl/macrosheets/", StringComparison.Ordinal))
            {
                throw SourceRefusal.Because(ImportErrorCodes.FileHasMacros);
            }

            if (name.StartsWith("xl/externallinks/", StringComparison.Ordinal) || name == "xl/connections.xml")
            {
                throw SourceRefusal.Because(ImportErrorCodes.FileExternalReference);
            }

            hasContentTypes |= name == "[content_types].xml";
            declared += entry.Length;

            if (declared > ImportLimits.MaxExpandedBytes)
            {
                throw SourceRefusal.Because(ImportErrorCodes.FileExpandsTooMuch);
            }
        }

        if (!hasContentTypes)
        {
            throw SourceRefusal.Because(ImportErrorCodes.FileUnreadable);
        }

        // The headers' claims are not trusted: the bytes the decompressor really produces are counted, across all entries.
        long actual = 0;
        var buffer = new byte[81_920];

        foreach (var entry in archive.Entries)
        {
            var name = Normalize(entry.FullName);
            var inspected = name == "[content_types].xml" || name.EndsWith(".rels", StringComparison.Ordinal);

            using var stream = entry.Open();
            using var kept = inspected ? new MemoryStream() : null;
            int read;

            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                actual += read;

                if (actual > ImportLimits.MaxExpandedBytes)
                {
                    throw SourceRefusal.Because(ImportErrorCodes.FileExpandsTooMuch);
                }

                if (kept is not null)
                {
                    if (kept.Length + read > MaxInspectedPartBytes)
                    {
                        throw SourceRefusal.Because(ImportErrorCodes.FileUnreadable);
                    }

                    kept.Write(buffer, 0, read);
                }
            }

            if (kept is null)
            {
                continue;
            }

            kept.Position = 0;

            if (name == "[content_types].xml")
            {
                CheckContentTypes(kept);
            }
            else
            {
                CheckRelationships(kept);
            }
        }
    }

    private static void CheckContentTypes(Stream part)
    {
        using var reader = SafeXml(part);

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName is not ("Override" or "Default"))
            {
                continue;
            }

            var contentType = reader.GetAttribute("ContentType") ?? string.Empty;

            if (contentType.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase)
                || contentType.Contains("vbaProject", StringComparison.OrdinalIgnoreCase))
            {
                throw SourceRefusal.Because(ImportErrorCodes.FileHasMacros);
            }
        }
    }

    private static void CheckRelationships(Stream part)
    {
        using var reader = SafeXml(part);

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Relationship")
            {
                continue;
            }

            var type = reader.GetAttribute("Type") ?? string.Empty;
            var external = string.Equals(reader.GetAttribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase);

            // A hyperlink is external by nature and is read as data (BR-IE-3); anything else pointing outside the package is a reference.
            if (type.EndsWith("/externalLink", StringComparison.OrdinalIgnoreCase)
                || type.EndsWith("/externalLinkPath", StringComparison.OrdinalIgnoreCase)
                || (external && !type.EndsWith("/hyperlink", StringComparison.OrdinalIgnoreCase)))
            {
                throw SourceRefusal.Because(ImportErrorCodes.FileExternalReference);
            }
        }
    }

    /// <summary>An XML reader with DTDs prohibited and no resolver: an XXE or an entity bomb is an <see cref="XmlException"/>.</summary>
    private static XmlReader SafeXml(Stream stream) =>
        XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxInspectedPartBytes,
                CloseInput = false,
            });

    private static string Normalize(string fullName) => fullName.Replace('\\', '/').TrimStart('/').ToLowerInvariant();

    private static bool StartsWith(byte[] content, byte[] signature) =>
        content.Length >= signature.Length && content.AsSpan(0, signature.Length).SequenceEqual(signature);
}
