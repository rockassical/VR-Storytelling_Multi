public enum NHEJPhase
{
    WaitingForPlayers,
    Phase0_Trigger,
    Phase1_KuBinding,
    Phase2_DNAPKcs,
    Phase3_Trimming,
    Phase4_GapFill,
    Phase5_Alignment,
    Phase6_Ligation,
    Phase7_Cleanup,
    Phase8_Assessment,
    Complete
}

public enum DSBScenario
{
    BluntEnds,          // No overhangs — Phase 3 skipped entirely for both players
    LeftOverhangOnly,   // Only left end (Player 1) needs trimming
    RightOverhangOnly,  // Only right end (Player 2) needs trimming
    BothOverhangs       // Both players trim
}
