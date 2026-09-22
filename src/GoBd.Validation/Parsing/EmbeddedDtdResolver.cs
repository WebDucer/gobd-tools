using System.Xml;
using GoBd.Validation.Dtd;

namespace GoBd.Validation.Parsing;

/// <summary>
/// Resolves the document type declaration to the application's own canonical grammar, and
/// refuses everything else.
/// </summary>
/// <remarks>
/// The export never gets to influence the grammar it is judged against, and no reference in
/// <c>index.xml</c> can reach the filesystem or the network. A relative system identifier — any
/// bare <c>.dtd</c> file name, whichever of the two the producer declared — yields the embedded
/// canonical bytes. An absolute one keeps its own scheme after resolution and is refused. See
/// design.md D1.
/// </remarks>
public sealed class EmbeddedDtdResolver : XmlResolver
{
    /// <summary>Scheme used for the synthetic base URI of <c>index.xml</c>.</summary>
    internal const string Scheme = "gobd-export";

    /// <summary>Base URI to pass to the reader so relative references stay in our own scheme.</summary>
    internal static Uri BaseUri { get; } = new($"{Scheme}:///index.xml");

    /// <summary>System identifiers that were refused, in the order they were encountered.</summary>
    public IReadOnlyList<string> RefusedReferences => refused;

    private readonly List<string> refused = [];

    /// <inheritdoc />
    public override Uri ResolveUri(Uri? baseUri, string? relativeUri) =>
        base.ResolveUri(baseUri ?? BaseUri, relativeUri);

    /// <inheritdoc />
    public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        ArgumentNullException.ThrowIfNull(absoluteUri);

        // A relative system identifier resolved against our synthetic base keeps our scheme.
        // Anything else came in absolute and is by definition external.
        if (!string.Equals(absoluteUri.Scheme, Scheme, StringComparison.Ordinal))
        {
            refused.Add(absoluteUri.OriginalString);
            throw new ExternalReferenceRefusedException(absoluteUri.OriginalString);
        }

        return CanonicalDtd.OpenRead();
    }
}

/// <summary>Raised when <c>index.xml</c> points its document type declaration outside the export.</summary>
public sealed class ExternalReferenceRefusedException : Exception
{
    /// <summary>Creates the exception for the given system identifier.</summary>
    public ExternalReferenceRefusedException(string systemId)
        : base($"Refused to resolve external reference '{systemId}'.") => SystemId = systemId;

    /// <summary>Creates the exception with a default message.</summary>
    public ExternalReferenceRefusedException()
        : base("Refused to resolve an external reference.") => SystemId = string.Empty;

    /// <summary>Creates the exception with a custom message and inner exception.</summary>
    public ExternalReferenceRefusedException(string message, Exception innerException)
        : base(message, innerException) => SystemId = string.Empty;

    /// <summary>The system identifier that was refused.</summary>
    public string SystemId { get; }
}
