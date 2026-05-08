using System.Numerics;

namespace Zhengyan.HNSW;

public static class DistanceCacheLimits
{
	private static int _maxArrayLength = 268435456;

	public static int MaxArrayLength
	{
		get
		{
			return _maxArrayLength;
		}
		set
		{
			_maxArrayLength = NextPowerOf2((uint)value);
		}
	}

	private static int NextPowerOf2(uint x)
	{
		uint num = BitOperations.RoundUpToPowerOf2(x);
		if (num > 268435456)
		{
			return 268435456;
		}
		return (int)num;
	}
}

