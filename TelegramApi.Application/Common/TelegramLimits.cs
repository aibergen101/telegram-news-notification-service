namespace TelegramApi.Application.Common;

public static class TelegramLimits
{
    public const int MaxMessageLength = 4096;
    public const int MaxCaptionLength = 1024;

    public const int MaxButtons = 100;
    public const int MaxButtonTextLength = 64;
    public const int MaxButtonUrlLength = 2048;
    public const int MaxLinkLength = 2048;

    public const int MaxAlbumItems = 10;
    public const int MaxFileNameLength = 255;
    public const long MaxPhotoBytes = 10L * 1024 * 1024;
    public const long MaxDocumentBytes = 50L * 1024 * 1024;

    public const int MaxPollQuestionLength = 300;
    public const int MaxPollOptionLength = 100;
    public const int MinPollOptions = 2;
    public const int MaxPollOptions = 10;
    public const int MinPollDurationSeconds = 5;
    public const int MaxPollDurationSeconds = 600;
}
