namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
internal sealed class NullableContextAttribute : Attribute
{
    public NullableContextAttribute(byte flag)
    {
    }
}

[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
internal sealed class NullableAttribute : Attribute
{
    public NullableAttribute(byte flag)
    {
    }

    public NullableAttribute(byte[] flags)
    {
    }
}
