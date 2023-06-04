namespace OpenSpace;

internal sealed class Statistics : IStatistics
{
    public double PreRenderAddMeshDuration { get; set; }
    
    public double PreRenderGetEntitiesDuration { get; set; }
    
    public double PreRenderClearMeshDuration { get; set; }
    
    public int PreRenderMeshCount { get; set; }
    
    public long UpdateTransformSystemDuration { get; set; }
}