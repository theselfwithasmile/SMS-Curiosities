using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Variants;

public class Spawner : MonoBehaviour
{
    [SerializeField] List<BaseZone> zones;

    // GameFlowManager owns which zone is current (it survives reloads and picks randomly without
    // repeats); only this class knows how many zones actually exist, so it reports that count back
    // every Start(). Absent (not yet wired into the scene), falls back to zone 0.
    int CurrentZoneIndex => GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentZoneIndex % zones.Count : 0;

    // Start is called before the first frame update
    void Start()
    {
        // Always keep GameFlowManager's zone count current, even on a Menu-state load, so it's
        // ready the moment Start/Retry/NextLevel/CycleZone needs to draw a zone.
        GameFlowManager.Instance?.RegisterZoneCount(zones.Count);

        // Gated on GameFlowManager so a fresh load lands on the menu instead of dropping straight
        // into a zone - absent (not yet wired into the scene), falls back to the old always-on
        // behaviour. A reload triggered mid-game (R-key zone cycling, Retry, Next Level) already
        // carries State == Playing across on GameFlowManager's DontDestroyOnLoad instance.
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
