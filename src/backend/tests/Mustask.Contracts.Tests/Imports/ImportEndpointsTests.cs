using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.Features.Imports;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Imports.UploadFile;
using Mustask.Domain.Identity;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Source;
using Mustask.Shared.Exceptions;

namespace Mustask.Contracts.Tests.Imports;

/// <summary>
/// mod-024 api-contract § 3.1: the upload route's own work (the form read by hand, the refusals it maps, the Created answer) and the route
/// table, run against the endpoint's request delegate with a hand-built request - no HTTP host, no database.
/// </summary>
public sealed class ImportEndpointsTests
{
    private const string OrganizationId = "00000000-0000-0000-0000-0000000000a1";

    [Fact]
    public void The_route_table_holds_every_import_route_under_the_organization()
    {
        var routes = Routes(Build(OrganizationRole.Admin, new RecordingReader()))
            .Select(route => $"{route.Method} {route.Pattern.RawText}")
            .ToList();

        const string prefix = "/api/organizations/{organizationId}/imports";
        string[] expected =
        [
            $"GET {prefix}/catalogue",
            $"GET {prefix}/templates",
            $"GET {prefix}/templates/{{templateId}}",
            $"GET {prefix}/templates/{{templateId}}/columns",
            $"POST {prefix}/templates",
            $"PUT {prefix}/templates/{{templateId}}",
            $"POST {prefix}/templates/{{templateId}}/copy",
            $"DELETE {prefix}/templates/{{templateId}}",
            $"POST {prefix}/files",
            $"GET {prefix}/files/{{fileId}}/columns",
            $"POST {prefix}/files/{{fileId}}/values",
            $"POST {prefix}/files/{{fileId}}/chain-sample",
            $"POST {prefix}/templates/{{templateId}}/dry-runs",
            $"GET {prefix}/dry-runs/{{dryRunId}}",
            $"GET {prefix}/dry-runs/{{dryRunId}}/report",
        ];

        Assert.Equal(expected.Order(), routes.Order());
    }

    [Fact]
    public async Task An_upload_is_stored_and_answers_created_with_the_columns_link()
    {
        var reader = new RecordingReader();
        var app = Build(OrganizationRole.Admin, reader);
        var form = new MultipartFormDataContent
        {
            { new StringContent("Sheet2"), "sheet" },
            { new StringContent("3"), "headerRow" },
            { new StringContent("4"), "firstDataRow" },
            { new StringContent("windows-1251"), "encoding" },
            { new StringContent(";"), "delimiter" },
            { FileContent("a,b"u8.ToArray()), "file", "C:\\fakepath\\items.csv" },
        };

        var (context, body) = await RequestAsync(form);
        await Upload(app).Invoke(context);

        Assert.Equal((int)HttpStatusCode.Created, context.Response.StatusCode);
        Assert.StartsWith($"/api/organizations/{OrganizationId}/imports/files/", context.Response.Headers.Location.ToString());
        Assert.EndsWith("/columns", context.Response.Headers.Location.ToString());
        Assert.Contains("items.csv", Encoding.UTF8.GetString(body.ToArray()));
        Assert.Equal("items.csv", reader.FileName);
        Assert.Equal(new SourceReadOptions("Sheet2", 3, 4, "windows-1251", ";", ImportLimits.SampleRows), reader.Options);
        Assert.Equal("a,b"u8.ToArray(), reader.Content);
    }

    [Fact]
    public async Task An_upload_with_no_options_reads_the_first_sheet_from_the_first_row_and_detects_the_rest()
    {
        var reader = new RecordingReader();
        var form = new MultipartFormDataContent { { FileContent([1, 2, 3]), "file", "items.xlsx" } };

        var (context, _) = await RequestAsync(form);
        await Upload(Build(OrganizationRole.Admin, reader)).Invoke(context);

        Assert.Equal(new SourceReadOptions(null, 1, null, null, null, ImportLimits.SampleRows), reader.Options);
    }

    [Fact]
    public async Task A_headerRow_that_is_not_a_number_falls_back_to_the_default()
    {
        var reader = new RecordingReader();
        var form = new MultipartFormDataContent { { new StringContent("abc"), "headerRow" }, { FileContent([1]), "file", "x.csv" } };

        var (context, _) = await RequestAsync(form);
        await Upload(Build(OrganizationRole.Admin, reader)).Invoke(context);

        Assert.Equal(1, reader.Options?.HeaderRow);
    }

    [Fact]
    public async Task A_body_that_is_not_a_form_is_an_unsupported_format()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues["organizationId"] = OrganizationId;
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream("{}"u8.ToArray());

        await AssertRefused(ImportErrorCodes.FileUnsupportedFormat, context, Build(OrganizationRole.Admin, new RecordingReader()));
    }

    [Fact]
    public async Task A_body_over_the_cap_is_a_file_too_large_before_it_is_read()
    {
        var (context, _) = await RequestAsync(new MultipartFormDataContent { { FileContent([1]), "file", "x.csv" } });
        context.Request.ContentLength = ImportLimits.MaxFileBytes + (2L * 1024 * 1024);

        await AssertRefused(ImportErrorCodes.FileTooLarge, context, Build(OrganizationRole.Admin, new RecordingReader()));
    }

    [Fact]
    public async Task A_file_over_the_limit_with_no_declared_length_is_a_file_too_large()
    {
        var reader = new RecordingReader();
        var (context, _) = await RequestAsync(
            new MultipartFormDataContent { { FileContent(new byte[ImportLimits.MaxFileBytes + 1]), "file", "big.csv" } });
        context.Request.ContentLength = null;

        await AssertRefused(ImportErrorCodes.FileTooLarge, context, Build(OrganizationRole.Admin, reader));
        Assert.Null(reader.Options);
    }

    [Fact]
    public async Task A_form_without_the_file_part_is_unreadable()
    {
        var (context, _) = await RequestAsync(new MultipartFormDataContent { { new StringContent("1"), "headerRow" } });

        await AssertRefused(ImportErrorCodes.FileUnreadable, context, Build(OrganizationRole.Admin, new RecordingReader()));
    }

    [Fact]
    public async Task A_truncated_multipart_body_is_unreadable()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues["organizationId"] = OrganizationId;
        context.Request.ContentType = "multipart/form-data; boundary=xyz";
        context.Request.Body = new MemoryStream("--xyz\r\nContent-Disposition: form-data; name=\"file\"; filename=\"a.csv\"\r\n\r\nabc"u8.ToArray());

        await AssertRefused(ImportErrorCodes.FileUnreadable, context, Build(OrganizationRole.Admin, new RecordingReader()));
    }

    [Fact]
    public async Task A_stream_that_fails_while_the_client_is_still_connected_is_unreadable()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues["organizationId"] = OrganizationId;
        context.Request.ContentType = "multipart/form-data; boundary=xyz";
        context.Request.Body = new FailingStream();

        await AssertRefused(ImportErrorCodes.FileUnreadable, context, Build(OrganizationRole.Admin, new RecordingReader()));
    }

    [Fact]
    public async Task The_body_cap_is_raised_to_the_upload_limit_when_the_server_allows_it()
    {
        var (context, _) = await RequestAsync(new MultipartFormDataContent { { FileContent([1]), "file", "x.csv" } });
        var limit = new SettableBodyLimit();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(limit);

        await Upload(Build(OrganizationRole.Admin, new RecordingReader())).Invoke(context);

        Assert.Equal(ImportLimits.MaxFileBytes + (1024 * 1024), limit.MaxRequestBodySize);
    }

    [Fact]
    public async Task A_read_only_body_cap_is_left_alone()
    {
        var (context, _) = await RequestAsync(new MultipartFormDataContent { { FileContent([1]), "file", "x.csv" } });
        var limit = new SettableBodyLimit { IsReadOnly = true };
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(limit);

        await Upload(Build(OrganizationRole.Admin, new RecordingReader())).Invoke(context);

        Assert.Equal(1L, limit.MaxRequestBodySize);
    }

    [Fact]
    public async Task A_viewer_is_refused_before_the_body_is_read()
    {
        var (context, _) = await RequestAsync(new MultipartFormDataContent { { FileContent([1]), "file", "x.csv" } });
        var reader = new RecordingReader();

        var refusal = await Assert.ThrowsAsync<ForbiddenException>(() => Upload(Build(OrganizationRole.Viewer, reader)).Invoke(context));

        Assert.Equal(ImportErrorCodes.AccessForbidden, refusal.Code);
        Assert.Null(reader.Options);
    }

    [Fact]
    public async Task An_upload_to_an_unaddressable_organization_is_not_found()
    {
        var (context, _) = await RequestAsync(new MultipartFormDataContent { { FileContent([1]), "file", "x.csv" } });
        context.Request.RouteValues["organizationId"] = "not-an-id";

        await Assert.ThrowsAsync<NotFoundException>(() => Upload(Build(OrganizationRole.Admin, new RecordingReader())).Invoke(context));
    }

    [Fact]
    public void A_dry_run_request_carries_the_optional_file()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, new DryRunRequest(id).FileId);
        Assert.Null(new DryRunRequest(null).FileId);
    }

    private static async Task AssertRefused(string reason, HttpContext context, WebApplication app)
    {
        var refusal = await Assert.ThrowsAsync<ValidationException>(() => Upload(app).Invoke(context));

        Assert.Equal(ImportErrorCodes.FileRefused, refusal.Code);
        Assert.Equal(reason, refusal.Errors[SourceRefusal.Field].Single().Code);
    }

    private static ByteArrayContent FileContent(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new("application/octet-stream");

        return content;
    }

    private static async Task<(DefaultHttpContext Context, MemoryStream ResponseBody)> RequestAsync(MultipartFormDataContent form)
    {
        var bytes = await form.ReadAsByteArrayAsync();
        var response = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Request.RouteValues["organizationId"] = OrganizationId;
        context.Request.ContentType = form.Headers.ContentType!.ToString();
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        context.Response.Body = response;

        return (context, response);
    }

    private static RequestDelegate Upload(WebApplication app)
    {
        var endpoint = Routes(app).Single(route => route.Method == "POST" && route.Pattern.RawText!.EndsWith("/imports/files", StringComparison.Ordinal));

        return context =>
        {
            context.RequestServices = app.Services;

            return endpoint.RequestDelegate!(context);
        };
    }

    private static IEnumerable<(string Method, RoutePattern Pattern, RequestDelegate? RequestDelegate)> Routes(WebApplication app) =>
        ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => (
                endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single(),
                endpoint.RoutePattern,
                (RequestDelegate?)endpoint.RequestDelegate));

    private static WebApplication Build(OrganizationRole role, RecordingReader reader)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IOrganizationAccess>(new FixedAccess(role));
        builder.Services.AddSingleton<ICurrentUser>(new FixedUser());
        builder.Services.AddSingleton<IUnitOfWork>(new PassThroughUnitOfWork());
        builder.Services.AddSingleton<IClock>(new FixedClock());
        builder.Services.AddSingleton<IImportFileRepository>(new NullFileRepository());
        builder.Services.AddSingleton<ISourceFileReader>(reader);
        builder.Services.AddSingleton<UploadFileHandler>();

        // The other routes are never invoked here, but the endpoint table binds their handlers as services when it is built.
        foreach (var handler in typeof(UploadFileHandler).Assembly.GetTypes().Where(type =>
                     type.IsClass && type.Name.EndsWith("Handler", StringComparison.Ordinal) && type != typeof(UploadFileHandler)
                     && type.Namespace?.StartsWith("Mustask.Application.Features.Imports", StringComparison.Ordinal) == true))
        {
            builder.Services.AddSingleton(handler);
        }

        var app = builder.Build();
        app.MapImportEndpoints();

        return app;
    }

    private sealed class RecordingReader : ISourceFileReader
    {
        public SourceReadOptions? Options { get; private set; }

        public string? FileName { get; private set; }

        public byte[]? Content { get; private set; }

        public SourceTable Read(byte[] content, string fileName, SourceReadOptions options)
        {
            Options = options;
            FileName = fileName;
            Content = content;

            return new SourceTable(SourceFormat.Csv, [], null, options.HeaderRow, options.HeaderRow + 1, "utf-8", ",", true, true, null, [], [], 0);
        }
    }

    private sealed class FixedAccess(OrganizationRole role) : IOrganizationAccess
    {
        public OrganizationRole Role => role;

        public bool Holds(string code) => OrganizationRoleBundles.Holds(role, code);
    }

    private sealed class FixedUser : ICurrentUser
    {
        public string DisplayName => "Test";

        public string SubjectId => "user-1";
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);
    }

    private sealed class PassThroughUnitOfWork : IUnitOfWork
    {
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken) =>
            action(cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NullFileRepository : IImportFileRepository
    {
        public Task<ImportFile?> FindAsync(Guid organizationId, string userId, Guid fileId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult<ImportFile?>(null);

        public void Add(ImportFile file)
        {
        }

        public Task DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SettableBodyLimit : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly { get; init; }

        public long? MaxRequestBodySize { get; set; } = 1;
    }

    private sealed class FailingStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("connection reset");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("connection reset");

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
