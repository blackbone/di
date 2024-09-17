# DI Package for Unity

This is a simple as possible DI for unity. Nothing special.

## Installation
Simply add as git package in unity or a line below to `manifest.json`

`"com.blackbone.di": "https://github.com/blackbone/di.git#v0.3.0"`

## Usage

### Container creation and registration

```csharp
public interface IMyService { }
public class MyService : IMyService { }

public static class Bootstrap
{
    private static IContainer _container;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Start() {
        Application.wantsToQuit += OnAppQuitting;

        // create app container
        _container = IContainer.Create();

        // register singleton by it's type
        // this one will be created on IContainer.Run
        _container.Register<MyService>();

        // register implementations
        // MyService one will be created on IContainer.Run
        container.Register<IMyService, MyService>();

        // register instance
        container.Register(new MyService());

        // register instance as IMyService
        container.Register<IMyService>(new MyService());

        // register all stuff manually...

        // this will finalize configuration and create all instances
        container.Run();
    }

    // attempt to graceful shutdown =)
    private static void OnAppQuitting() {
        Application.wantsToQuit -= OnAppQuitting;

        _container.Dispose();
        _container = null;
    }
}
```

---

Also non generic API are supported to register things dinamically.

```csharp

public interface ISoundSettings { }
public interface IGraphicSettings { }
public interface IGameSettings { }
public class Settings : ISoundSettings, IGraphicSettings, IGameSettings { }

public static class Bootstrap
{
    private static IContainer _container;

    // container creation 

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Start() {
        Application.wantsToQuit += OnAppQuitting;

        // create app container
        _container = IContainer.Create();

        RegisterSettings(_container);

        // this will finalize configuration and create all instances
        container.Run();
    }

    // attempt to graceful shutdown =)
    private static void OnAppQuitting() {
        Application.wantsToQuit -= OnAppQuitting;

        _container.Dispose();
        _container = null;
    }

    private static void RegisterSettings(IContainer container) {
        // load settings from resources (for example it's JSON)
        var settingsAsset = Resources.Load<TextAsset>("settings");
        var settings = JsonUtility.FromJson<Settings>(settingsAsset.text);
        Resources.UnloadAsset(settingsAsset);

        // get explicit interfaces so app config can be registered as multi-key singleton
        var interfaces = settings.GetType().GetInterfaces(false);
        foreach (var api in interfaces)
            container.Register(api, settings);

        // and also register settings as self typed
        container.Register(settings);
    }
}

```

### Disposing

Just dispose container like this:

```csharp
_container.Dispose();
_container = null;
```

## Injection

Injection is simple and accessible anywhere where you have access to IContainer instance.
There's two types of incection which can be used:
1. **Constructor Injection**
2. **Property / Field Injection**

### Constructor Injection

Constructor injection is suitable and preferable for pure .NET ojects and works as following:

```csharp
class MyClass {
    private readonly IMyService service;
    private int value;
    
    public MyClass(IMyService service) {
        this.service = service;
        this.value = 0;
    }

    public MyClass(IMyService service, int value) {
        this.service = service;
        this.value = value;
    }
    
    // other logic
}

// part of some user code 
// ...

// instead of creating through constructor - use Resolve method
// this will create new MyClass and inject IMyService into it using constructor only with IMyService and return true
_container.Resolve(out MyClass instance);

// instead of creating through constructor - use Resolve method
// this will create new MyClass and inject IMyService into it using constructor with IMyService and int and return true
_container.Resolve(out MyClass instance, 10);

// instead of creating through constructor - use Resolve method
// this will return false because no constructor found
_container.Resolve(out MyClass instance, "10");
```

---

Typeless API also presented:
```csharp
class MyClass {
    private readonly IMyService service;
    private int value;
    
    private MyClass(IMyService service) {
        this.service = service;
        this.value = 0;
    }

    private MyClass(IMyService service, int value) {
        this.service = service;
        this.value = value;
    }
    
    // other logic
}

// part of some user code 
// ...

// instead of creating through constructor - use Resolve method
// this will create new MyClass and inject IMyService into it using constructor only with IMyService
// will return MyClass instance
var instance = _container.Resolve(type) as MyClass;

// this will create new MyClass and inject IMyService into it using constructor only with IMyService and int
// will return MyClass instance
var instance = _container.Resolve(type, 10) as MyClass;

// this will create new MyClass and inject IMyService into it using constructor only with IMyService and int
// will throw UnsupportedCtorParameterException
var instance = _container.Resolve(type, "10") as MyClass;
```

Also notice that constructors are private - it's intended behaviour and allowed semantics.

### Members Injection

Member injection is applied in two cases:
1. When using constructor injection - after success instance creation it will be applied to instance
2. When you have and instance and need to resolve dependencies. For example - on MonoBehaviour scripts.

To make injectable member you need to define field or property and call `_container.Inject` as in example below:
```csharp
interface IMyService { }
class MyService : IMyService { }
class MyClass
{
    [Inject] private IMyService serviceField;
    [Inject] private IMyService serviceProperty { get; set; }

    // other logic
}


// part of some user code 
// ...

// get instance of MyClass somewhere, no matter where from
var myClassInstance = new MyClass();

// inject IMyService with container 
_container.Inject(myClassInstance);

// how serviceField and serviceProperty injected with instance of MyService (sure if it previously has been registered)

```

## Transiency

Trnaciency feature allow to inject and control temporary objects with DI manner.
Following example will be more descriptive:

```csharp
// define transient data type
class SessionTransientData
{
    // used as key to access transient objects
    public static readonly object Key = new();
    public string[] players;
}
// define our type which we'll resolve or inject, don't matter
class GameMode
{
    // when defining fields or properties need to initialize it with proper keys
    public readonly TransientAccess<int> level = new(0); // numbers and enums are ok
    public readonly TransientAccess<string> sceneName = new("sceneName"); // strings are ok
    public readonly TransientAccess<SessionTransientData> transientData = new(SessionTransientData.Key); // objects are ok
    // any type of keys are ok because they used in dictionary under the hood

    // other logic
}

// part of some user code 
// ...

// register transient objects with same keys as they used in fields
var sceneTransientData = new SessionTransientData { players = new[] { "John", "Steve", "Tom" } };
_container.RegisterTransientObject(1, 100);
_container.RegisterTransientObject("sceneName", "TeamDeathmatchPort");
_container.RegisterTransientObject(SessionTransientData.Key, sceneTransientData);
        
// get instance of MyClass somewhere, no matter where from
var game = new GameMode();
        
// inject IMyService with container 
 _container.Inject(game);

// at this point transient objects will be injected and data can be used
if (game.level.HasValue)
    Debug.Log(nameof(game.level) + ": " + game.level.Value); // level: 100
if (game.sceneName.HasValue)
    Debug.Log(nameof(game.sceneName) + ": " + game.sceneName.Value); // sceneName: "TeamDeathmatchPort"
if (game.transientData.HasValue)
    Debug.Log(nameof(game.transientData) + ": " + string.Join(", ", game.transientData.Value.players)); // players: John, Steve, Tom 
// don't forget to check HasValue each time before direct access to Value
        
// unregistering is straightforward too
_container.UnregisterTransientObject(1);
_container.UnregisterTransientObject("sceneName");
 _container.UnregisterTransientObject(SessionTransientData.Key, out SessionTransientData data);
// this will remove transient objects from lookup
// registered values can be returned with overload with out parameter to give ability to dispose objects
// also returning bool indicating if it successfull or not
```

## Roadmap

No fixed roadmap there but i'm interested in your requests and ideas.

Here's couple short term ideas:

- [ ] Code generated resolver
- [ ] Code generated installers \ bootstrap registration
- [ ] Custom resolution factories
- [ ] Integrations with other (my) packages
- [ ] Improve the docs \ samples \ describe some general use cases

## Contribution and Feedback

Feel free to create PR and Issues.


# Support

<a href="https://www.buymeacoffee.com/blackbone"><img src="https://img.buymeacoffee.com/button-api/?text=Buy me a whisky&emoji=🥃&slug=blackbone&button_colour=FFDD00&font_colour=000000&font_family=Cookie&outline_colour=000000&coffee_colour=ffffff" /></a>