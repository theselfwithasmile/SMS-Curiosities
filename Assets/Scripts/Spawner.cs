using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Variants;

public class Spawner : MonoBehaviour
{
    [SerializeField] List<BaseZone> zones;

    // GameFlowManager owns the ever-incrementing zone counter (it survives reloads and is shared
    // with NextLevel's difficulty bump); only this class knows how many zones actually exist, so
    // the wrap-around lives here. Absent (not yet wired into the scene), falls back to zone 0.
    int CurrentZoneIndex => GameFlowManager.Instance != null ? GameFlowManager.Instance.ZoneIndex % zones.Count : 0;

    // Start is called before the first frame update
    void Start()
    {
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
