using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Variants;

public class Spawner : MonoBehaviour
{
    [SerializeField] List<BaseZone> zones;
    
    int CurrentZoneIndex => GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentZoneIndex % zones.Count : 0;
    
    void Start()
    {
        GameFlowManager.Instance?.RegisterZoneCount(zones.Count);

        //gated on GameFlowManager so a fresh load lands on the menu
        if (GameFlowManager.Instance == null || GameFlowManager.Instance.State == FlowState.Playing)
        {
            ActivateZone(CurrentZoneIndex);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            GameFlowManager.Instance?.CycleZone();
        }
    }

    void ActivateZone(int idx)
    {
        //disable all zone variants (they share one GameObject)
        foreach (BaseZone zone in zones) zone.enabled = false;

        //enable the requested one
        zones[idx].gameObject.SetActive(true);
        zones[idx].enabled = true;
    }
}
