namespace TelegramApi.Domain;

public class MediaFile
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public MediaType MediaType { get; set; }
    public string TgFileId { get; set; }
    public Guid PublicationId { get; set; }
    public Publication Publication { get; set; }
}
