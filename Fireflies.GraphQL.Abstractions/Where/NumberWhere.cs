namespace Fireflies.GraphQL.Abstractions.Where;

public abstract class NumberWhere<T> : Where<T> where T : struct {
    public T? GreaterThan { get; set; }
    public T? GreaterThanOrEq { get; set; }
    public T? LessThan { get; set; }
    public T? LessThanOrEq { get; set; }

}