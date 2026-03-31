namespace Snapp.Shared.DTOs.Notification;

public class DigestPreviewResponse
{
    public Dictionary<string, int> Categories { get; set; } = new();

    public List<NotificationResponse> TopItems { get; set; } = new();

    public DigestQuestionItem? PendingQuestion { get; set; }
}

public class DigestQuestionItem
{
    public string QuestionId { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;
    public List<string> Choices { get; set; } = new();
    public string AnswerUrl { get; set; } = string.Empty;
}
