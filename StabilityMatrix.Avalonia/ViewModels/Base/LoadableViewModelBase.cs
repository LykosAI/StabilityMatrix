using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using NLog;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

namespace StabilityMatrix.Avalonia.ViewModels.Base;

[JsonDerivedType(typeof(StackExpanderViewModel), StackExpanderViewModel.ModuleKey)]
[JsonDerivedType(typeof(SamplerCardViewModel), SamplerCardViewModel.ModuleKey)]
[JsonDerivedType(typeof(FreeUCardViewModel), FreeUCardViewModel.ModuleKey)]
[JsonDerivedType(typeof(UpscalerCardViewModel), UpscalerCardViewModel.ModuleKey)]
[JsonDerivedType(typeof(ControlNetCardViewModel), ControlNetCardViewModel.ModuleKey)]
[JsonDerivedType(typeof(PromptExpansionCardViewModel), PromptExpansionCardViewModel.ModuleKey)]
[JsonDerivedType(typeof(ExtraNetworkCardViewModel), ExtraNetworkCardViewModel.ModuleKey)]
[JsonDerivedType(typeof(LayerDiffuseCardViewModel), LayerDiffuseCardViewModel.ModuleKey)]
[JsonDerivedType(typeof(FaceDetailerViewModel), FaceDetailerViewModel.ModuleKey)]
[JsonDerivedType(typeof(DiscreteModelSamplingCardViewModel), DiscreteModelSamplingCardViewModel.ModuleKey)]
[JsonDerivedType(typeof(RescaleCfgCardViewModel), RescaleCfgCardViewModel.ModuleKey)]
[JsonDerivedType(typeof(PlasmaNoiseCardViewModel), PlasmaNoiseCardViewModel.ModuleKey)]
[JsonDerivedType(typeof(FreeUModule))]
[JsonDerivedType(typeof(HiresFixModule))]
[JsonDerivedType(typeof(FluxHiresFixModule))]
[JsonDerivedType(typeof(UpscalerModule))]
[JsonDerivedType(typeof(ControlNetModule))]
[JsonDerivedType(typeof(SaveImageModule))]
[JsonDerivedType(typeof(PromptExpansionModule))]
[JsonDerivedType(typeof(LoraModule))]
[JsonDerivedType(typeof(LayerDiffuseModule))]
[JsonDerivedType(typeof(FaceDetailerModule))]
[JsonDerivedType(typeof(FluxGuidanceModule))]
[JsonDerivedType(typeof(DiscreteModelSamplingModule))]
[JsonDerivedType(typeof(RescaleCfgModule))]
[JsonDerivedType(typeof(PlasmaNoiseModule))]
public abstract class LoadableViewModelBase : ViewModelBase, IJsonLoadableState
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly Type[] SerializerIgnoredTypes = { typeof(ICommand), typeof(IRelayCommand) };

    private static readonly string[] SerializerIgnoredNames = { nameof(HasErrors) };

    private static readonly JsonSerializerOptions SerializerOptions =
        new() { IgnoreReadOnlyProperties = true };

    private static bool ShouldIgnoreProperty(PropertyInfo property)
    {
        // Skip if read-only and not IJsonLoadableState
        if (property.SetMethod is null && !typeof(IJsonLoadableState).IsAssignableFrom(property.PropertyType))
        {
            Logger.ConditionalTrace("Skipping {Property} - read-only", property.Name);
            return true;
        }
        // Check not JsonIgnore
        if (property.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Length > 0)
        {
            Logger.ConditionalTrace("Skipping {Property} - has [JsonIgnore]", property.Name);
            return true;
        }
        // Check not excluded type
        if (SerializerIgnoredTypes.Contains(property.PropertyType))
        {
            Logger.ConditionalTrace(
                "Skipping {Property} - serializer ignored type {Type}",
                property.Name,
                property.PropertyType
            );
            return true;
        }
        // Check not ignored name
        if (SerializerIgnoredNames.Contains(property.Name, StringComparer.Ordinal))
        {
            Logger.ConditionalTrace("Skipping {Property} - serializer ignored name", property.Name);
            return true;
        }

        return false;
    }

    /// <summary>
    /// True if we should include property without checking exclusions
    /// </summary>
    private static bool ShouldIncludeProperty(PropertyInfo property)
    {
        // Has JsonIncludeAttribute
        if (property.GetCustomAttributes(typeof(JsonIncludeAttribute), true).Length > 0)
        {
            Logger.ConditionalTrace("Including {Property} - has [JsonInclude]", property.Name);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Load the state of this view model from a JSON object.
    /// The default implementation is a mirror of <see cref="SaveStateToJsonObject"/>.
    /// For the following properties on this class, we will try to set from the JSON object:
    /// <list type="bullet">
    /// <item>Public</item>
    /// <item>Not read-only</item>
    /// <item>Not marked with [JsonIgnore]</item>
    /// <item>Not a type within the SerializerIgnoredTypes</item>
    /// <item>Not a name within the SerializerIgnoredNames</item>
    /// </list>
    /// </summary>
    public virtual void LoadStateFromJsonObject(JsonObject state)
    {
        // Get all of our properties using reflection
        var properties = GetType().GetProperties();
        Logger.ConditionalTrace("Serializing {Type} with {Count} properties", GetType(), properties.Length);

        foreach (var property in properties)
        {
            var name = property.Name;

            // If JsonPropertyName provided, use that as the key
            if (
                property.GetCustomAttributes(typeof(JsonPropertyNameAttribute), true).FirstOrDefault()
                is JsonPropertyNameAttribute jsonPropertyName
            )
            {
                Logger.ConditionalTrace(
                    "Deserializing {Property} ({Type}) with JsonPropertyName {JsonPropertyName}",
                    property.Name,
                    property.PropertyType,
                    jsonPropertyName.Name
                );
                name = jsonPropertyName.Name;
            }

            // Check if property is in the JSON object
            if (!state.TryGetPropertyValue(name, out var value))
            {
                Logger.ConditionalTrace("Skipping {Property} - not in JSON object", property.Name);
                continue;
            }

            // Check if we should ignore this property
            if (!ShouldIncludeProperty(property) && ShouldIgnoreProperty(property))
            {
                continue;
            }

            // For types that also implement IJsonLoadableState, defer to their load implementation
            if (typeof(IJsonLoadableState).IsAssignableFrom(property.PropertyType))
            {
                Logger.ConditionalTrace(
                    "Loading {Property} ({Type}) with IJsonLoadableState",
                    property.Name,
                    property.PropertyType
                );

                // Value must be non-null
                if (value is null)
                {
                    throw new InvalidOperationException(
                        $"Property {property.Name} is IJsonLoadableState but value to be loaded is null"
                    );
                }

                // Check if the current object at this property is null
                if (property.GetValue(this) is not IJsonLoadableState propertyValue)
                {
                    // If null, it must have a default constructor
                    if (property.PropertyType.GetConstructor(Type.EmptyTypes) is not { } constructorInfo)
                    {
                        throw new InvalidOperationException(
                            $"Property {property.Name} is IJsonLoadableState but current object is null and has no default constructor"
                        );
                    }

                    // Create a new instance and set it
                    propertyValue = (IJsonLoadableState)constructorInfo.Invoke(null);
                    property.SetValue(this, propertyValue);
                }

                // Load the state from the JSON object
                propertyValue.LoadStateFromJsonObject(value.AsObject());
            }
            else
            {
                Logger.ConditionalTrace("Loading {Property} ({Type})", property.Name, property.PropertyType);

                var propertyValue = value.Deserialize(property.PropertyType, SerializerOptions);
                property.SetValue(this, propertyValue);
            }
        }
    }

    /// <summary>
    /// Saves the state of this view model to a JSON object.
    /// The default implementation uses reflection to
    /// save all properties that are:
    /// <list type="bullet">
    /// <item>Public</item>
    /// <item>Not read-only</item>
    /// <item>Not marked with [JsonIgnore]</item>
    /// <item>Not a type within the SerializerIgnoredTypes</item>
    /// <item>Not a name within the SerializerIgnoredNames</item>
    /// </list>
    /// </summary>
    public virtual JsonObject SaveStateToJsonObject()
    {
        // Get all of our properties using reflection.
        var properties = GetType().GetProperties();
        Logger.ConditionalTrace("Serializing {Type} with {Count} properties", GetType(), properties.Length);

        // Create a JSON object to store the state.
        var state = new JsonObject();

        // Serialize each property marked with JsonIncludeAttribute.
        foreach (var property in properties)
        {
            if (!ShouldIncludeProperty(property) && ShouldIgnoreProperty(property))
            {
                continue;
            }

            var name = property.Name;

            // If JsonPropertyName provided, use that as the key.
            if (
                property.GetCustomAttributes(typeof(JsonPropertyNameAttribute), true).FirstOrDefault()
                is JsonPropertyNameAttribute jsonPropertyName
            )
            {
                Logger.ConditionalTrace(
                    "Serializing {Property} ({Type}) with JsonPropertyName {JsonPropertyName}",
                    property.Name,
                    property.PropertyType,
                    jsonPropertyName.Name
                );
                name = jsonPropertyName.Name;
            }

            // For types that also implement IJsonLoadableState, defer to their implementation.
            if (typeof(IJsonLoadableState).IsAssignableFrom(property.PropertyType))
            {
                Logger.ConditionalTrace(
                    "Serializing {Property} ({Type}) with IJsonLoadableState",
                    property.Name,
                    property.PropertyType
                );
                var value = property.GetValue(this);
                if (value is not null)
                {
                    var model = (IJsonLoadableState)value;
                    var modelState = model.SaveStateToJsonObject();
                    state.Add(name, modelState);
                }
            }
            else
            {
                Logger.ConditionalTrace(
                    "Serializing {Property} ({Type})",
                    property.Name,
                    property.PropertyType
                );
                var value = property.GetValue(this);
                if (value is not null)
                {
                    state.Add(name, JsonSerializer.SerializeToNode(value, SerializerOptions));
                }
            }
        }

        return state;
    }

    public virtual void LoadStateFromJsonObject(JsonObject state, int version)
    {
        LoadStateFromJsonObject(state);
    }

    /// <summary>
    /// Serialize a model to a JSON object.
    /// </summary>
    protected static JsonObject SerializeModel<T>(T model)
    {
        var node = JsonSerializer.SerializeToNode(model);
        return node?.AsObject()
            ?? throw new NullReferenceException("Failed to serialize state to JSON object.");
    }

    /// <summary>
    /// Deserialize a model from a JSON object.
    /// </summary>
    protected static T DeserializeModel<T>(JsonObject state)
    {
        return state.Deserialize<T>()
            ?? throw new NullReferenceException("Failed to deserialize state from JSON object.");
    }
}
