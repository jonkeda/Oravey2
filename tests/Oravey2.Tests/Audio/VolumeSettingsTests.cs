using Oravey2.Core.Audio;
using Oravey2.Core.Framework.Events;

namespace Oravey2.Tests.Audio;

public class VolumeSettingsTests
{
    private readonly EventBus _bus = new();
    private readonly VolumeSettings _vol;

    public VolumeSettingsTests()
    {
        _vol = new VolumeSettings(_bus);
    }

    [Fact]
    public void DefaultVolumesAreOne()
    {
        foreach (AudioCategory cat in Enum.GetValues<AudioCategory>())
            Assert.Equal(1f, _vol.GetVolume(cat));
    }

    [Fact]
    public void SetVolumeStoresValue()
    {
        _vol.SetVolume(AudioCategory.SFX, 0.5f);
        Assert.Equal(0.5f, _vol.GetVolume(AudioCategory.SFX));
    }

    [Fact]
    public void SetVolumeClampsAboveOne()
    {
        _vol.SetVolume(AudioCategory.Music, 1.5f);
        Assert.Equal(1f, _vol.GetVolume(AudioCategory.Music));
    }

    [Fact]
    public void SetVolumeClampsBelowZero()
    {
        _vol.SetVolume(AudioCategory.Ambient, -0.5f);
        Assert.Equal(0f, _vol.GetVolume(AudioCategory.Ambient));
    }

    [Fact]
    public void EffectiveVolumeIsCategoryTimesMaster()
    {
        _vol.SetVolume(AudioCategory.Master, 0.5f);
        _vol.SetVolume(AudioCategory.SFX, 0.8f);

        Assert.Equal(0.4f, _vol.GetEffectiveVolume(AudioCategory.SFX), 3);
    }

    [Fact]
    public void EffectiveMasterReturnsRaw()
    {
        _vol.SetVolume(AudioCategory.Master, 0.7f);
        Assert.Equal(0.7f, _vol.GetEffectiveVolume(AudioCategory.Master), 3);
    }

    [Fact]
    public void VolumeChangedEventOnChange()
    {
        VolumeChangedEvent? received = null;
        _bus.Subscribe<VolumeChangedEvent>(e => received = e);

        _vol.SetVolume(AudioCategory.Voice, 0.3f);

        Assert.NotNull(received);
        Assert.Equal(AudioCategory.Voice, received.Value.Category);
        Assert.Equal(1f, received.Value.OldVolume);
        Assert.Equal(0.3f, received.Value.NewVolume, 3);
    }

    [Fact]
    public void NoEventWhenSameValue()
    {
        VolumeChangedEvent? received = null;
        _bus.Subscribe<VolumeChangedEvent>(e => received = e);

        _vol.SetVolume(AudioCategory.Music, 1f); // already 1

        Assert.Null(received);
    }

    [Fact]
    public void MultipleCategoriesIndependent()
    {
        _vol.SetVolume(AudioCategory.SFX, 0.3f);
        _vol.SetVolume(AudioCategory.Music, 0.6f);

        Assert.Equal(0.3f, _vol.GetVolume(AudioCategory.SFX), 3);
        Assert.Equal(0.6f, _vol.GetVolume(AudioCategory.Music), 3);
    }

    [Fact]
    public void MasterZeroMutesAllEffective()
    {
        _vol.SetVolume(AudioCategory.Master, 0f);

        Assert.Equal(0f, _vol.GetEffectiveVolume(AudioCategory.SFX));
        Assert.Equal(0f, _vol.GetEffectiveVolume(AudioCategory.Music));
        Assert.Equal(0f, _vol.GetEffectiveVolume(AudioCategory.Ambient));
        Assert.Equal(0f, _vol.GetEffectiveVolume(AudioCategory.Voice));
    }
}
