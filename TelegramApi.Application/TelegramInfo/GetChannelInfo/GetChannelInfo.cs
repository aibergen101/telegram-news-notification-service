using FluentValidation;
using TelegramApi.Application.Common;

namespace TelegramApi.Application.TelegramInfo.GetChannelInfo;

public record GetChannelInfoQuery(string Alias);

public class GetChannelInfoValidator : AbstractValidator<GetChannelInfoQuery>
{
    public GetChannelInfoValidator()
    {
        RuleFor(x => x.Alias).MustBeChannelAlias();
    }
}

public class GetChannelInfoHandler
{
    public static Task<ChannelInfo> Handle(GetChannelInfoQuery query, ITelegramService telegram, CancellationToken ct) =>
        telegram.GetChannelInfoAsync(ChannelAlias.Normalize(query.Alias), ct);
}
