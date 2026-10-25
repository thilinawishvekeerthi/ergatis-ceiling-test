using System.Collections.Concurrent;
using System.Reflection;
using Talent.Security.Contracts;

namespace Talent.Security.Masking;

/// <summary>Masks DTO properties by their <see cref="FieldClassificationAttribute"/>. Works on records with a primary constructor.</summary>
internal sealed class FieldMasker : IFieldMasker
{
    private static readonly ConcurrentDictionary<Type, Plan> Plans = new();

    public T Mask<T>(T value, AccessGrant grant, bool isOwnRecord = false)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(grant);

        var plan = Plans.GetOrAdd(typeof(T), BuildPlan);
        var args = new object?[plan.Members.Length];
        for (var i = 0; i < args.Length; i++)
        {
            var m = plan.Members[i];
            args[i] = grant.CanRead(m.FieldClass, isOwnRecord) ? m.Property.GetValue(value) : m.Default;
        }

        return (T)plan.Constructor.Invoke(args);
    }

    private static Plan BuildPlan(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var constructor = type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault(c => c.GetParameters().All(p => Find(properties, p.Name) is not null))
            ?? throw new InvalidOperationException($"{type.Name} needs a constructor whose parameters match its properties.");

        var members = constructor.GetParameters().Select(p =>
        {
            var property = Find(properties, p.Name)!;
            var fieldClass = property.GetCustomAttribute<FieldClassificationAttribute>()?.FieldClass ?? FieldClass.TalentConfidential;
            var canBeNull = !property.PropertyType.IsValueType || Nullable.GetUnderlyingType(property.PropertyType) is not null;
            if (fieldClass != FieldClass.Public && !canBeNull)
            {
                throw new InvalidOperationException(
                    $"{type.Name}.{property.Name} is {fieldClass} but cannot be null, so it cannot be masked. Make it nullable.");
            }

            return new Member(property, fieldClass, canBeNull ? null : Activator.CreateInstance(property.PropertyType));
        }).ToArray();

        return new Plan(constructor, members);
    }

    private static PropertyInfo? Find(PropertyInfo[] properties, string? name) =>
        properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    private sealed record Member(PropertyInfo Property, FieldClass FieldClass, object? Default);

    private sealed record Plan(ConstructorInfo Constructor, Member[] Members);
}
