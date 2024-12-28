namespace DurableTask.AspNetCore.Utilize;

sealed class CoreDataConverterShim : Core.Serializing.DataConverter
{
    private readonly Microsoft.DurableTask.DataConverter converter;

    public CoreDataConverterShim(Microsoft.DurableTask.DataConverter converter)
    {
        this.converter = converter;
    }

    public override object Deserialize(string data, Type objectType)
    {
        return converter.Deserialize(data, objectType);
    }

    public override string Serialize(object value)
    {
        return converter.Serialize(value);
    }

    public override string Serialize(object value, bool formatted)
    {
        return Serialize(value);
    }
}