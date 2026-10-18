namespace Kanimambo.Ordering.Domain;

/// <summary>Prototype: an object that can produce an independent deep copy of itself.</summary>
public interface IPrototype<out T>
{
    T DeepClone();
}
