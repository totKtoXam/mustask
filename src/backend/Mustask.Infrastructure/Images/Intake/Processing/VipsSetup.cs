using System.Reflection;
using System.Runtime.InteropServices;

namespace Mustask.Infrastructure.Images.Intake.Processing;

/// <summary>
/// One-time start of libvips (plan-backend § 5; the spike, status.md Log). A missing native library fails here, at the start of
/// the API, with a clear message, not at the first upload. The caches are off (a server holds no pictures between requests), the
/// thread count is configuration, and every loader is blocked except the four buffer loaders of the stored formats, so a file
/// that slips past the header reader (SVG, HEIF, PDF, ...) still cannot reach another decoder.
/// </summary>
internal static class VipsSetup
{
    private static readonly object Gate = new();
    private static bool _initialized;

    /// <summary>The loaders left open: PNG, JPEG, WebP and (in this build) the nsgif GIF buffer loader.</summary>
    private static readonly string[] AllowedLoaders =
    [
        "VipsForeignLoadPngBuffer",
        "VipsForeignLoadJpegBuffer",
        "VipsForeignLoadWebpBuffer",
        "VipsForeignLoadNsgifBuffer",
    ];

    public static void EnsureInitialized(int vipsThreads)
    {
        lock (Gate)
        {
            if (_initialized)
            {
                return;
            }

            NativeLibrary.SetDllImportResolver(typeof(VipsSetup).Assembly, Resolve);

            try
            {
                var major = NetVips.NetVips.Version(0);
                var minor = NetVips.NetVips.Version(1);
                if (major < 8 || (major == 8 && minor < 18))
                {
                    throw new InvalidOperationException($"libvips {major}.{minor} is too old: 8.18 or newer is required.");
                }

                NetVips.Cache.Max = 0;
                NetVips.Cache.MaxMem = 0;
                NetVips.Cache.MaxFiles = 0;
                NetVips.NetVips.Concurrency = Math.Max(1, vipsThreads);

                NetVips.NetVips.BlockUntrusted = true;
                Native.vips_operation_block_set("VipsForeignLoad", 1);
                foreach (var loader in AllowedLoaders)
                {
                    Native.vips_operation_block_set(loader, 0);
                }
            }
            catch (Exception exception) when (exception is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or TypeInitializationException)
            {
                throw new InvalidOperationException(
                    "The libvips native library could not be loaded (the NetVips.Native package for this platform). The image store cannot start.",
                    exception);
            }

            _initialized = true;
        }
    }

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != Native.Library)
        {
            return IntPtr.Zero;
        }

        string[] candidates = OperatingSystem.IsWindows()
            ? ["libvips-42.dll", "libvips-42"]
            : OperatingSystem.IsMacOS() ? ["libvips.42.dylib", "libvips-42"] : ["libvips.so.42", "libvips-42"];

        foreach (var candidate in candidates)
        {
            if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }

    private static class Native
    {
        public const string Library = "vips";

        // NetVips exposes no per-class block: the two libvips calls are made directly.
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern void vips_operation_block_set([MarshalAs(UnmanagedType.LPStr)] string name, int state);
    }
}
