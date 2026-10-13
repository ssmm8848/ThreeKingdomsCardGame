using System.Collections.Generic;
using UnityEngine;

public static class IdentityAllocator
{
    // 5人场身份配置：1执令者 1护令者 2逆契者 1游离者
    private static readonly IdentityType[] FivePlayerConfig = new IdentityType[]
    {
        IdentityType.Commander,
        IdentityType.Protector,
        IdentityType.Rebel,
        IdentityType.Rebel,
        IdentityType.Wanderer,
    };

    // 随机洗牌并返回身份分配
    public static IdentityType[] AllocateFivePlayer()
    {
        List<IdentityType> identities = new List<IdentityType>(FivePlayerConfig);
        Shuffle(identities);
        return identities.ToArray();
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
