namespace MvcControllerScanner.Models;

/// <summary>
/// Action返回语句信息
/// </summary>
public class ReturnStatementInfo
{
    public string ReturnType { get; set; } = ""; // Forbid/NotFound/View/Redirect/Other
    public string SourceCode { get; set; } = "";
    public string RedirectTarget { get; set; } = null!;
    public string ModelType { get; set; } = null!;
    public Dictionary<string, object> ExampleModel { get; set; } = [];
    public Dictionary<string, string> ModelRawAssignments { get; set; } = [];
}

