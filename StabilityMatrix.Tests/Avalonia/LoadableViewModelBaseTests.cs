using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Base;

#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Tests.Avalonia;

// Example subclass
public class TestLoadableViewModel : LoadableViewModelBase
{
    [JsonInclude]
    public string? Included { get; set; }

    public int Id { get; set; }

    [JsonIgnore]
    public int Ignored { get; set; }
}

public class TestLoadableViewModelReadOnly : LoadableViewModelBase
{
    public int ReadOnly { get; }

    public TestLoadableViewModelReadOnly(int readOnly)
    {
        ReadOnly = readOnly;
    }
}

public class TestLoadableViewModelReadOnlyLoadable : LoadableViewModelBase
{
    public TestLoadableViewModel ReadOnlyLoadable { get; } = new();
}

public partial class TestLoadableViewModelObservable : LoadableViewModelBase
{
    [ObservableProperty]
    [property: JsonIgnore]
    private string? title;

    [ObservableProperty]
    private int id;

    [RelayCommand]
    private void TestCommand()
    {
        throw new NotImplementedException();
    }
}

public class TestLoadableViewModelNestedInterface : LoadableViewModelBase
{
    public IJsonLoadableState? NestedState { get; set; }
}

public class TestLoadableViewModelNested : LoadableViewModelBase
{
    public TestLoadableViewModel? NestedState { get; set; }
}

[TestClass]
public class LoadableViewModelBaseTests
{
    [TestMethod]
    public void TestSaveStateToJsonObject_JsonIgnoreAttribute()
    {
        var vm = new TestLoadableViewModel
        {
            Included = "abc",
            Id = 123,
            Ignored = 456,
        };

        var state = vm.SaveStateToJsonObject();

        // [JsonInclude] and not marked property should be serialized.
        // Ignored property should be ignored.
        Assert.AreEqual(2, state.Count);
        Assert.AreEqual("abc", state["Included"].Deserialize<string>());
        Assert.AreEqual(123, state["Id"].Deserialize<int>());
    }

    [TestMethod]
    public void TestSaveStateToJsonObject_Observable()
    {
        // Mvvm ObservableProperty should be serialized.
        var vm = new TestLoadableViewModelObservable { Title = "abc", Id = 123, };
        var state = vm.SaveStateToJsonObject();

        // Title should be ignored since it has [JsonIgnore]
        // Command should be ignored from excluded type rules
        // Id should be serialized

        Assert.AreEqual(1, state.Count);
        Assert.AreEqual(123, state["Id"].Deserialize<int>());
    }

    [TestMethod]
    public void TestSaveStateToJsonObject_IJsonLoadableState()
    {
        // Properties of type IJsonLoadableState should be serialized by calling their
        // SaveStateToJsonObject method.

        // Make a mock IJsonLoadableState
        var mockState = Substitute.For<IJsonLoadableState>();

        var vm = new TestLoadableViewModelNestedInterface { NestedState = mockState };

        // Serialize
        var state = vm.SaveStateToJsonObject();

        // Check results
        Assert.AreEqual(1, state.Count);

        // Check that SaveStateToJsonObject was called
        mockState.Received().SaveStateToJsonObject();
    }

    [TestMethod]
    public void TestLoadStateFromJsonObject()
    {
        // Simple round trip save / load
        var vm = new TestLoadableViewModel
        {
            Included = "abc",
            Id = 123,
            Ignored = 456,
        };

        var state = vm.SaveStateToJsonObject();

        // Create a new instance and load the state
        var vm2 = new TestLoadableViewModel();
        vm2.LoadStateFromJsonObject(state);

        // Check [JsonInclude] and not marked property was loaded
        Assert.AreEqual("abc", vm2.Included);
        Assert.AreEqual(123, vm2.Id);
        // Check ignored property was not loaded
        Assert.AreEqual(0, vm2.Ignored);
    }

    [TestMethod]
    public void TestLoadStateFromJsonObject_Nested_DefaultCtor()
    {
        // Round trip save / load with nested IJsonLoadableState property
        var nested = new TestLoadableViewModel
        {
            Included = "abc",
            Id = 123,
            Ignored = 456,
        };

        var vm = new TestLoadableViewModelNested { NestedState = nested };

        var state = vm.SaveStateToJsonObject();

        // Create a new instance with null NestedState, rely on default ctor
        var vm2 = new TestLoadableViewModelNested();
        vm2.LoadStateFromJsonObject(state);

        // Check nested state was loaded
        Assert.IsNotNull(vm2.NestedState);

        var loadedNested = (TestLoadableViewModel)vm2.NestedState;
        Assert.AreEqual("abc", loadedNested.Included);
        Assert.AreEqual(123, loadedNested.Id);
        Assert.AreEqual(0, loadedNested.Ignored);
    }

    [TestMethod]
    public void TestLoadStateFromJsonObject_Nested_Existing()
    {
        // Round trip save / load with nested IJsonLoadableState property
        var nested = new TestLoadableViewModel
        {
            Included = "abc",
            Id = 123,
            Ignored = 456,
        };

        var vm = new TestLoadableViewModelNestedInterface { NestedState = nested };

        var state = vm.SaveStateToJsonObject();

        // Create a new instance with existing NestedState
        var vm2 = new TestLoadableViewModelNestedInterface
        {
            NestedState = new TestLoadableViewModel()
        };
        vm2.LoadStateFromJsonObject(state);

        // Check nested state was loaded
        Assert.IsNotNull(vm2.NestedState);

        var loadedNested = (TestLoadableViewModel)vm2.NestedState;
        Assert.AreEqual("abc", loadedNested.Included);
        Assert.AreEqual(123, loadedNested.Id);
        Assert.AreEqual(0, loadedNested.Ignored);
    }

    [TestMethod]
    public void TestLoadStateFromJsonObject_ReadOnly()
    {
        var vm = new TestLoadableViewModelReadOnly(456);

        var state = vm.SaveStateToJsonObject();

        // Check no properties were serialized
        Assert.AreEqual(0, state.Count);

        // Create a new instance and load the state
        var vm2 = new TestLoadableViewModelReadOnly(123);
        vm2.LoadStateFromJsonObject(state);

        // Read only property should have been ignored
        Assert.AreEqual(123, vm2.ReadOnly);
    }

    [TestMethod]
    public void TestLoadStateFromJsonObject_ReadOnlyLoadable()
    {
        var vm = new TestLoadableViewModelReadOnlyLoadable
        {
            ReadOnlyLoadable = { Included = "abc-123" }
        };

        var state = vm.SaveStateToJsonObject();

        // Check readonly loadable property was serialized
        Assert.AreEqual(1, state.Count);
        Assert.AreEqual(
            "abc-123",
            state["ReadOnlyLoadable"].Deserialize<TestLoadableViewModel>()!.Included
        );
    }
}
