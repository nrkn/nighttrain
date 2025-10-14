
// so we don't have to implement empty methods on subclasses
public class ModSubsystemBase : IModSubsystem
{
    public virtual void Start() { }

    public virtual void Stop() { }

    public virtual void Tick() { }
}
