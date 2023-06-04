namespace OpenSpace;

public interface IStatistics
{
    double PreRenderAddMeshDuration { get; set; }
    
    double PreRenderGetEntitiesDuration { get; set; }
    
    double PreRenderClearMeshDuration { get; set; }

    int PreRenderMeshCount { get; set; }

    long UpdateTransformSystemDuration { get; set; }
}