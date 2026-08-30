namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 影灯使用已经按实际最大生命统一缩放的阶段额度，禁止 Power 系统再次按多人倍率放大。
/// </summary>
public sealed class ShadowTomorinDamageGatePower : EnemyMaxDamageReceivedPower
{
    public override bool ShouldScaleInMultiplayer => false;
}
