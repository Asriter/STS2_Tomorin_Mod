using MegaCrit.Sts2.Core.Models;
using System.Globalization;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 保存敌人卡牌跨实例共享且不可变的完整语义模板。
/// </summary>
public sealed class EnemyCardDefinition
{
    /// <summary>
    /// 创建一项不可变敌人卡牌定义。
    /// </summary>
    /// <param name="cardId">稳定卡牌定义标识。</param>
    /// <param name="cardModel">只用于牌面、本地化与规范变量的原版模型。</param>
    /// <param name="tags">参与指标匹配的标签集合。</param>
    /// <param name="scoreProfile">本体执行一次的软锁评分档案。</param>
    /// <param name="materialRequests">按执行顺序排列的不可变素材请求。</param>
    /// <param name="materialRequestProgramIds">与素材请求对应、参与兼容指纹的有序稳定程序标识。</param>
    /// <param name="lifecycle">至少成功执行一次后的去向。</param>
    /// <param name="failureDisposition">一次也未执行时的去向。</param>
    /// <param name="tokenTiming">作词结果进入执行流程的时机。</param>
    /// <param name="composeResultCardId">可选作词结果卡牌定义标识。</param>
    /// <param name="effects">投影与真实结算共享的有序效果程序。</param>
    /// <param name="effectProgramIds">在效果节点尚未构造时仍可显式提供的有序效果程序标识。</param>
    /// <param name="customExecutionTiming">兼容旧执行模板的自定义步骤时机。</param>
    /// <param name="descriptionOverride">只用于敌人 Intent 卡面的可信富文本描述；空串沿用原版描述。</param>
    public EnemyCardDefinition(
        EnemyCardId cardId,
        CardModel cardModel,
        EnemyCardTag tags,
        EnemyCardScoreProfile scoreProfile,
        IEnumerable<EnemyMaterialRequest>? materialRequests = null,
        IEnumerable<string>? materialRequestProgramIds = null,
        EnemyCardLifecycle lifecycle = EnemyCardLifecycle.Discard,
        EnemyCardFailureDisposition failureDisposition = EnemyCardFailureDisposition.Discard,
        EnemyCardTokenTiming tokenTiming = EnemyCardTokenTiming.None,
        EnemyCardId? composeResultCardId = null,
        IEnumerable<IEnemyCardEffectNode>? effects = null,
        IEnumerable<string>? effectProgramIds = null,
        EnemyCardCustomExecutionTiming customExecutionTiming = EnemyCardCustomExecutionTiming.AfterBaseEffects,
        string descriptionOverride = "")
    {
        DescriptionOverride = descriptionOverride ?? throw new ArgumentNullException(nameof(descriptionOverride));

        if (!cardId.IsValid)
        {
            throw new ArgumentException("敌人卡牌定义必须具有有效稳定标识。", nameof(cardId));
        }

        CardId = cardId;
        CardModel = cardModel ?? throw new ArgumentNullException(nameof(cardModel));
        Tags = tags;
        ScoreProfile = scoreProfile ?? throw new ArgumentNullException(nameof(scoreProfile));
        MaterialRequests = Array.AsReadOnly((materialRequests ?? []).ToArray());
        MaterialRequestProgramIds = BuildMaterialProgramIds(MaterialRequests, materialRequestProgramIds);
        Lifecycle = lifecycle;
        FailureDisposition = failureDisposition;
        TokenTiming = tokenTiming;
        ComposeResultCardId = composeResultCardId;
        Effects = Array.AsReadOnly((effects ?? []).ToArray());
        EffectProgramIds = BuildEffectProgramIds(Effects, effectProgramIds);
        CustomExecutionTiming = customExecutionTiming;

        if (TokenTiming == EnemyCardTokenTiming.None && ComposeResultCardId is not null)
        {
            throw new ArgumentException("具有作词结果的定义必须指定非 None 的 Token 时机。", nameof(composeResultCardId));
        }

        if (TokenTiming != EnemyCardTokenTiming.None && ComposeResultCardId is null)
        {
            throw new ArgumentException("指定 Token 时机的定义必须提供作词结果 CardId。", nameof(composeResultCardId));
        }

        if (MaterialRequests.Count > 0 &&
            MaterialRequestProgramIds.Count > 0 &&
            MaterialRequests.Count != MaterialRequestProgramIds.Count)
        {
            throw new ArgumentException("素材请求对象与有序程序标识数量必须一致。", nameof(materialRequestProgramIds));
        }

        SemanticFingerprint = BuildSemanticFingerprint();
    }

    /// <summary>获取稳定卡牌定义标识。</summary>
    public EnemyCardId CardId { get; }

    /// <summary>获取只用于显示与规范变量的原版卡牌模型。</summary>
    public CardModel CardModel { get; }

    /// <summary>获取指标匹配标签。</summary>
    public EnemyCardTag Tags { get; }

    /// <summary>获取一次本体执行的评分档案。</summary>
    public EnemyCardScoreProfile ScoreProfile { get; }

    /// <summary>获取按顺序冻结的素材请求对象。</summary>
    public IReadOnlyList<EnemyMaterialRequest> MaterialRequests { get; }

    /// <summary>获取按顺序参与指纹的素材请求程序标识。</summary>
    public IReadOnlyList<string> MaterialRequestProgramIds { get; }

    /// <summary>获取成功执行后的生命周期。</summary>
    public EnemyCardLifecycle Lifecycle { get; }

    /// <summary>获取零成功次数时的去向。</summary>
    public EnemyCardFailureDisposition FailureDisposition { get; }

    /// <summary>获取作词结果进入流程的时机。</summary>
    public EnemyCardTokenTiming TokenTiming { get; }

    /// <summary>获取可选作词结果定义标识。</summary>
    public EnemyCardId? ComposeResultCardId { get; }

    /// <summary>获取投影与执行共享的有序效果节点。</summary>
    public IReadOnlyList<IEnemyCardEffectNode> Effects { get; }

    /// <summary>获取有序效果程序标识。</summary>
    public IReadOnlyList<string> EffectProgramIds { get; }

    /// <summary>获取兼容旧卡牌模板的自定义执行时机。</summary>
    public EnemyCardCustomExecutionTiming CustomExecutionTiming { get; }

    /// <summary>获取只用于敌人 Intent 卡面的可信富文本描述；空串表示沿用原版描述。</summary>
    public string DescriptionOverride { get; }

    /// <summary>获取覆盖全部执行语义且与对象地址无关的稳定定义指纹。</summary>
    public string SemanticFingerprint { get; }

    /// <summary>
    /// 复制并验证一组有序程序标识。
    /// </summary>
    /// <param name="programIds">可选程序标识序列。</param>
    /// <param name="parameterName">用于异常诊断的参数名。</param>
    /// <returns>不可修改的有序程序标识副本。</returns>
    private static IReadOnlyList<string> CopyProgramIds(IEnumerable<string>? programIds, string parameterName)
    {
        string[] copied = (programIds ?? []).ToArray();
        if (copied.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("程序标识不能为空。", parameterName);
        }

        return Array.AsReadOnly(copied);
    }

    /// <summary>
    /// 合并素材请求自带标识与显式标识，并拒绝顺序不一致。
    /// </summary>
    /// <param name="requests">已经冻结的有序素材请求。</param>
    /// <param name="explicitIds">可选显式素材程序标识。</param>
    /// <returns>参与定义指纹的最终有序素材程序标识。</returns>
    private static IReadOnlyList<string> BuildMaterialProgramIds(
        IReadOnlyList<EnemyMaterialRequest> requests,
        IEnumerable<string>? explicitIds)
    {
        string[] requestIds = requests.Select(request => request.ProgramId).ToArray();
        IReadOnlyList<string> supplied = CopyProgramIds(explicitIds, nameof(explicitIds));
        if (supplied.Count > 0 && requestIds.Length > 0 && !supplied.SequenceEqual(requestIds, StringComparer.Ordinal))
        {
            throw new ArgumentException("显式素材程序顺序必须与素材请求顺序一致。", nameof(explicitIds));
        }

        return supplied.Count > 0 ? supplied : Array.AsReadOnly(requestIds);
    }

    /// <summary>
    /// 合并效果节点自带标识与显式标识，并拒绝相互矛盾的顺序。
    /// </summary>
    /// <param name="effects">已经冻结的效果节点。</param>
    /// <param name="explicitIds">可选显式程序标识。</param>
    /// <returns>最终参与定义指纹的有序程序标识。</returns>
    private static IReadOnlyList<string> BuildEffectProgramIds(
        IReadOnlyList<IEnemyCardEffectNode> effects,
        IEnumerable<string>? explicitIds)
    {
        string[] nodeIds = effects.Select(effect => effect.ProgramId).ToArray();
        IReadOnlyList<string> supplied = CopyProgramIds(explicitIds, nameof(explicitIds));
        if (nodeIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("效果节点必须提供稳定 ProgramId。", nameof(effects));
        }

        if (supplied.Count > 0 && nodeIds.Length > 0 && !supplied.SequenceEqual(nodeIds, StringComparer.Ordinal))
        {
            throw new ArgumentException("显式效果程序顺序必须与效果节点顺序一致。", nameof(explicitIds));
        }

        return supplied.Count > 0 ? supplied : Array.AsReadOnly(nodeIds);
    }

    /// <summary>
    /// 按固定字段顺序构造不依赖运行时对象地址的语义指纹。
    /// </summary>
    /// <returns>用于注册兼容校验的定义指纹。</returns>
    private string BuildSemanticFingerprint()
    {
        string modelType = CardModel.GetType().AssemblyQualifiedName ?? CardModel.GetType().FullName ?? CardModel.GetType().Name;
        string modelId = CardModel.Id.ToString();
        string score = string.Join(
            ",",
            ScoreProfile.Attack.ToString(CultureInfo.InvariantCulture),
            ScoreProfile.Block.ToString(CultureInfo.InvariantCulture),
            ScoreProfile.OtherPersistentPower.ToString(CultureInfo.InvariantCulture),
            ScoreProfile.Strength.ToString(CultureInfo.InvariantCulture),
            ScoreProfile.Dexterity.ToString(CultureInfo.InvariantCulture),
            ScoreProfile.AtField.ToString(CultureInfo.InvariantCulture),
            ScoreProfile.Vulnerable.ToString(CultureInfo.InvariantCulture),
            ScoreProfile.OtherDebuff.ToString(CultureInfo.InvariantCulture),
            ScoreProfile.NormalCollection.ToString(CultureInfo.InvariantCulture),
            ScoreProfile.StarStone.ToString(CultureInfo.InvariantCulture),
            ScoreProfile.AbilityHint.ToString(CultureInfo.InvariantCulture),
            ScoreProfile.DeferredTokenHint.ToString(CultureInfo.InvariantCulture));
        string materials = string.Join("\u001f", MaterialRequestProgramIds);
        string effects = string.Join("\u001f", EffectProgramIds);
        return string.Join(
            "\u001e",
            CardId.Value,
            modelType,
            modelId,
            (int)Tags,
            score,
            materials,
            Lifecycle,
            FailureDisposition,
            TokenTiming,
            ComposeResultCardId?.Value ?? string.Empty,
            CustomExecutionTiming,
            effects);
    }
}
