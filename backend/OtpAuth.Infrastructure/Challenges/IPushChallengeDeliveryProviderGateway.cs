using OtpAuth.Application.Challenges;

namespace OtpAuth.Infrastructure.Challenges;

public interface IPushChallengeDeliveryProviderGateway
{
    string ProviderName { get; }

    Task<PushChallengeDispatchResult> DeliverAsync(
        PushChallengeDispatchRequest request,
        CancellationToken cancellationToken);
}
