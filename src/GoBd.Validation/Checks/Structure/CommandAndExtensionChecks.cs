using GoBd.Validation.Findings;

namespace GoBd.Validation.Checks.Structure;

/// <summary>
/// Surfaces every declared <c>Command</c> for human review.
/// </summary>
/// <remarks>
/// The standard defines <c>Command</c> as an operating-system command run around the import,
/// and real exports ship things like <c>uncompress.bat</c>. This validator never executes one,
/// and never resolves, opens or inspects the file it names — the command text is reported as
/// declared and nothing more. That is a security boundary, not an unimplemented feature.
/// </remarks>
public sealed class CommandCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes => [FindingCodes.CommandDeclared];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var command in context.DataSet.Commands)
        {
            yield return Finding.Create(FindingCodes.CommandDeclared, command.Location, "DataSet", command.Value).About(FindingScope.Export("DataSet"));
        }

        foreach (var medium in context.DataSet.Media)
        {
            foreach (var command in medium.Commands)
            {
                yield return Finding.Create(
                    FindingCodes.CommandDeclared, command.Location, medium.Name.Value, command.Value).About(FindingScope.Medium(medium.Name.Value));
            }
        }
    }
}

/// <summary>
/// Reports declared extensions and whether the file each names is actually present.
/// </summary>
public sealed class ExtensionCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes => [FindingCodes.ExtensionDeclared, FindingCodes.ExtensionFileMissing];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var extension in context.DataSet.Extensions)
        {
            // An extension's meaning is outside this standard, so its presence is an observation.
            yield return Finding.Create(
                FindingCodes.ExtensionDeclared, extension.Location, extension.Name.Value, extension.Url.Value).About(FindingScope.Extension(extension.Name.Value));

            var resolution = Sources.ExportPath.Resolve(extension.Url.Value);
            if (!resolution.IsResolved || context.Source.Find(resolution.EntryName) is null)
            {
                yield return Finding.Create(
                    FindingCodes.ExtensionFileMissing, extension.Url.Location,
                    extension.Name.Value, extension.Url.Value).About(FindingScope.Extension(extension.Name.Value));
            }
        }
    }
}
