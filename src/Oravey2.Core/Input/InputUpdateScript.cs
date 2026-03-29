using Oravey2.Core.Framework.Services;
using Oravey2.Core.Input;
using Stride.Engine;

namespace Oravey2.Core.Input;

/// <summary>
/// Script that updates the IInputProvider each frame from Stride's InputManager.
/// Attach to any persistent entity in the scene.
/// </summary>
public class InputUpdateScript : SyncScript
{
    private IInputProvider? _inputProvider;

    public override void Start()
    {
        if (ServiceLocator.Instance.TryGet<IInputProvider>(out var provider))
            _inputProvider = provider;
    }

    public override void Update()
    {
        _inputProvider?.Update(Input);
    }
}
