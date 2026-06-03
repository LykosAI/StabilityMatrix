namespace StabilityMatrix.Core.Models;

public class EnvVarKeyPair
{
    public string Key { get; set; }
    public string Value { get; set; }
    public bool IsEnabled { get; set; }

    public EnvVarKeyPair(string key = "", string value = "", bool isEnabled = true)
    {
        Key = key;
        Value = value;
        IsEnabled = isEnabled;
    }
}
