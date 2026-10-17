namespace Kanimambo.SharedKernel.Results;

/// <summary>
/// Lets generic code (e.g. pipeline behaviors) create a failed <typeparamref name="TSelf"/>
/// without reflection, through a static abstract member.
/// </summary>
public interface IResultFactory<TSelf>
    where TSelf : IResultFactory<TSelf>
{
    static abstract TSelf CreateFailure(Error error);
}
