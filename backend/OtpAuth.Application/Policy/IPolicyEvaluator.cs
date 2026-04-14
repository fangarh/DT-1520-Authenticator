namespace OtpAuth.Application.Policy;

public interface IPolicyEvaluator
{
    PolicyDecision Evaluate(PolicyContext context);
}
