namespace StabilityMatrix.Core.Models;

public class EnvVarKeyPair
{
    public string Key { get; set; }
    public string Value { get; set; }

    public EnvVarKeyPair(string key = "", string value = "")
    {
        Key = key;
        Value = value;
    }
}
