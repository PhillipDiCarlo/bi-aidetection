using AITool;
using Xunit;

namespace AITool.Tests;

public class CameraConstructionTests
{
    [Fact]
    public void NamedCameraConstructorDoesNotRecurseWhenNoDefaultObjectsExist()
    {
        // Used to stack-overflow: Update() -> Reset() -> Update() -> Reset() ... when the
        // default camera's relevant-object list was empty (fresh install, or a test host).
        var cam = new Camera("TestCam");
        Assert.Equal("TestCam", cam.Name);
        Assert.NotNull(cam.DefaultTriggeringObjects);
    }
}
