﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal.enc
{
	class cost
	{

		extern const ushort VP8LevelFixedCosts[2048];   // approximate cost per level
		extern const ushort VP8EntropyCost[256];        // 8bit fixed-point log(p)

		// Cost of coding one event with probability 'proba'.
		static WEBP_INLINE int VP8BitCost(int bit, byte proba) {
		  return !bit ? VP8EntropyCost[proba] : VP8EntropyCost[255 - proba];
		}

		// Level cost calculations
		extern const ushort VP8LevelCodes[MAX_VARIABLE_LEVEL][2];
		void VP8CalculateLevelCosts(VP8Proba* const proba);
		static WEBP_INLINE int VP8LevelCost(const ushort* const table, int level) {
		  return VP8LevelFixedCosts[level]
			   + table[(level > MAX_VARIABLE_LEVEL) ? MAX_VARIABLE_LEVEL : level];
		}

		// Mode costs
		extern const ushort VP8FixedCostsUV[4];
		extern const ushort VP8FixedCostsI16[4];
		extern const ushort VP8FixedCostsI4[NUM_BMODES][NUM_BMODES][NUM_BMODES];

		//------------------------------------------------------------------------------

		//------------------------------------------------------------------------------
		// Boolean-cost cost table

		const ushort VP8EntropyCost[256] = {
		  1792, 1792, 1792, 1536, 1536, 1408, 1366, 1280, 1280, 1216,
		  1178, 1152, 1110, 1076, 1061, 1024, 1024,  992,  968,  951,
		   939,  911,  896,  878,  871,  854,  838,  820,  811,  794,
		   786,  768,  768,  752,  740,  732,  720,  709,  704,  690,
		   683,  672,  666,  655,  647,  640,  631,  622,  615,  607,
		   598,  592,  586,  576,  572,  564,  559,  555,  547,  541,
		   534,  528,  522,  512,  512,  504,  500,  494,  488,  483,
		   477,  473,  467,  461,  458,  452,  448,  443,  438,  434,
		   427,  424,  419,  415,  410,  406,  403,  399,  394,  390,
		   384,  384,  377,  374,  370,  366,  362,  359,  355,  351,
		   347,  342,  342,  336,  333,  330,  326,  323,  320,  316,
		   312,  308,  305,  302,  299,  296,  293,  288,  287,  283,
		   280,  277,  274,  272,  268,  266,  262,  256,  256,  256,
		   251,  248,  245,  242,  240,  237,  234,  232,  228,  226,
		   223,  221,  218,  216,  214,  211,  208,  205,  203,  201,
		   198,  196,  192,  191,  188,  187,  183,  181,  179,  176,
		   175,  171,  171,  168,  165,  163,  160,  159,  156,  154,
		   152,  150,  148,  146,  144,  142,  139,  138,  135,  133,
		   131,  128,  128,  125,  123,  121,  119,  117,  115,  113,
		   111,  110,  107,  105,  103,  102,  100,   98,   96,   94,
			92,   91,   89,   86,   86,   83,   82,   80,   77,   76,
			74,   73,   71,   69,   67,   66,   64,   63,   61,   59,
			57,   55,   54,   52,   51,   49,   47,   46,   44,   43,
			41,   40,   38,   36,   35,   33,   32,   30,   29,   27,
			25,   24,   22,   21,   19,   18,   16,   15,   13,   12,
			10,    9,    7,    6,    4,    3
		};

		//------------------------------------------------------------------------------
		// Level cost tables

		// For each given level, the following table gives the pattern of contexts to
		// use for coding it (in [][0]) as well as the bit value to use for each
		// context (in [][1]).
		const ushort VP8LevelCodes[MAX_VARIABLE_LEVEL][2] = {
						  {0x001, 0x000}, {0x007, 0x001}, {0x00f, 0x005},
		  {0x00f, 0x00d}, {0x033, 0x003}, {0x033, 0x003}, {0x033, 0x023},
		  {0x033, 0x023}, {0x033, 0x023}, {0x033, 0x023}, {0x0d3, 0x013},
		  {0x0d3, 0x013}, {0x0d3, 0x013}, {0x0d3, 0x013}, {0x0d3, 0x013},
		  {0x0d3, 0x013}, {0x0d3, 0x013}, {0x0d3, 0x013}, {0x0d3, 0x093},
		  {0x0d3, 0x093}, {0x0d3, 0x093}, {0x0d3, 0x093}, {0x0d3, 0x093},
		  {0x0d3, 0x093}, {0x0d3, 0x093}, {0x0d3, 0x093}, {0x0d3, 0x093},
		  {0x0d3, 0x093}, {0x0d3, 0x093}, {0x0d3, 0x093}, {0x0d3, 0x093},
		  {0x0d3, 0x093}, {0x0d3, 0x093}, {0x0d3, 0x093}, {0x153, 0x053},
		  {0x153, 0x053}, {0x153, 0x053}, {0x153, 0x053}, {0x153, 0x053},
		  {0x153, 0x053}, {0x153, 0x053}, {0x153, 0x053}, {0x153, 0x053},
		  {0x153, 0x053}, {0x153, 0x053}, {0x153, 0x053}, {0x153, 0x053},
		  {0x153, 0x053}, {0x153, 0x053}, {0x153, 0x053}, {0x153, 0x053},
		  {0x153, 0x053}, {0x153, 0x053}, {0x153, 0x053}, {0x153, 0x053},
		  {0x153, 0x053}, {0x153, 0x053}, {0x153, 0x053}, {0x153, 0x053},
		  {0x153, 0x053}, {0x153, 0x053}, {0x153, 0x053}, {0x153, 0x053},
		  {0x153, 0x053}, {0x153, 0x053}, {0x153, 0x053}, {0x153, 0x153}
		};

		// fixed costs for coding levels, deduce from the coding tree.
		// This is only the part that doesn't depend on the probability state.
		const ushort VP8LevelFixedCosts[2048] = {
			 0,  256,  256,  256,  256,  432,  618,  630,
		   731,  640,  640,  828,  901,  948, 1021, 1101,
		  1174, 1221, 1294, 1042, 1085, 1115, 1158, 1202,
		  1245, 1275, 1318, 1337, 1380, 1410, 1453, 1497,
		  1540, 1570, 1613, 1280, 1295, 1317, 1332, 1358,
		  1373, 1395, 1410, 1454, 1469, 1491, 1506, 1532,
		  1547, 1569, 1584, 1601, 1616, 1638, 1653, 1679,
		  1694, 1716, 1731, 1775, 1790, 1812, 1827, 1853,
		  1868, 1890, 1905, 1727, 1733, 1742, 1748, 1759,
		  1765, 1774, 1780, 1800, 1806, 1815, 1821, 1832,
		  1838, 1847, 1853, 1878, 1884, 1893, 1899, 1910,
		  1916, 1925, 1931, 1951, 1957, 1966, 1972, 1983,
		  1989, 1998, 2004, 2027, 2033, 2042, 2048, 2059,
		  2065, 2074, 2080, 2100, 2106, 2115, 2121, 2132,
		  2138, 2147, 2153, 2178, 2184, 2193, 2199, 2210,
		  2216, 2225, 2231, 2251, 2257, 2266, 2272, 2283,
		  2289, 2298, 2304, 2168, 2174, 2183, 2189, 2200,
		  2206, 2215, 2221, 2241, 2247, 2256, 2262, 2273,
		  2279, 2288, 2294, 2319, 2325, 2334, 2340, 2351,
		  2357, 2366, 2372, 2392, 2398, 2407, 2413, 2424,
		  2430, 2439, 2445, 2468, 2474, 2483, 2489, 2500,
		  2506, 2515, 2521, 2541, 2547, 2556, 2562, 2573,
		  2579, 2588, 2594, 2619, 2625, 2634, 2640, 2651,
		  2657, 2666, 2672, 2692, 2698, 2707, 2713, 2724,
		  2730, 2739, 2745, 2540, 2546, 2555, 2561, 2572,
		  2578, 2587, 2593, 2613, 2619, 2628, 2634, 2645,
		  2651, 2660, 2666, 2691, 2697, 2706, 2712, 2723,
		  2729, 2738, 2744, 2764, 2770, 2779, 2785, 2796,
		  2802, 2811, 2817, 2840, 2846, 2855, 2861, 2872,
		  2878, 2887, 2893, 2913, 2919, 2928, 2934, 2945,
		  2951, 2960, 2966, 2991, 2997, 3006, 3012, 3023,
		  3029, 3038, 3044, 3064, 3070, 3079, 3085, 3096,
		  3102, 3111, 3117, 2981, 2987, 2996, 3002, 3013,
		  3019, 3028, 3034, 3054, 3060, 3069, 3075, 3086,
		  3092, 3101, 3107, 3132, 3138, 3147, 3153, 3164,
		  3170, 3179, 3185, 3205, 3211, 3220, 3226, 3237,
		  3243, 3252, 3258, 3281, 3287, 3296, 3302, 3313,
		  3319, 3328, 3334, 3354, 3360, 3369, 3375, 3386,
		  3392, 3401, 3407, 3432, 3438, 3447, 3453, 3464,
		  3470, 3479, 3485, 3505, 3511, 3520, 3526, 3537,
		  3543, 3552, 3558, 2816, 2822, 2831, 2837, 2848,
		  2854, 2863, 2869, 2889, 2895, 2904, 2910, 2921,
		  2927, 2936, 2942, 2967, 2973, 2982, 2988, 2999,
		  3005, 3014, 3020, 3040, 3046, 3055, 3061, 3072,
		  3078, 3087, 3093, 3116, 3122, 3131, 3137, 3148,
		  3154, 3163, 3169, 3189, 3195, 3204, 3210, 3221,
		  3227, 3236, 3242, 3267, 3273, 3282, 3288, 3299,
		  3305, 3314, 3320, 3340, 3346, 3355, 3361, 3372,
		  3378, 3387, 3393, 3257, 3263, 3272, 3278, 3289,
		  3295, 3304, 3310, 3330, 3336, 3345, 3351, 3362,
		  3368, 3377, 3383, 3408, 3414, 3423, 3429, 3440,
		  3446, 3455, 3461, 3481, 3487, 3496, 3502, 3513,
		  3519, 3528, 3534, 3557, 3563, 3572, 3578, 3589,
		  3595, 3604, 3610, 3630, 3636, 3645, 3651, 3662,
		  3668, 3677, 3683, 3708, 3714, 3723, 3729, 3740,
		  3746, 3755, 3761, 3781, 3787, 3796, 3802, 3813,
		  3819, 3828, 3834, 3629, 3635, 3644, 3650, 3661,
		  3667, 3676, 3682, 3702, 3708, 3717, 3723, 3734,
		  3740, 3749, 3755, 3780, 3786, 3795, 3801, 3812,
		  3818, 3827, 3833, 3853, 3859, 3868, 3874, 3885,
		  3891, 3900, 3906, 3929, 3935, 3944, 3950, 3961,
		  3967, 3976, 3982, 4002, 4008, 4017, 4023, 4034,
		  4040, 4049, 4055, 4080, 4086, 4095, 4101, 4112,
		  4118, 4127, 4133, 4153, 4159, 4168, 4174, 4185,
		  4191, 4200, 4206, 4070, 4076, 4085, 4091, 4102,
		  4108, 4117, 4123, 4143, 4149, 4158, 4164, 4175,
		  4181, 4190, 4196, 4221, 4227, 4236, 4242, 4253,
		  4259, 4268, 4274, 4294, 4300, 4309, 4315, 4326,
		  4332, 4341, 4347, 4370, 4376, 4385, 4391, 4402,
		  4408, 4417, 4423, 4443, 4449, 4458, 4464, 4475,
		  4481, 4490, 4496, 4521, 4527, 4536, 4542, 4553,
		  4559, 4568, 4574, 4594, 4600, 4609, 4615, 4626,
		  4632, 4641, 4647, 3515, 3521, 3530, 3536, 3547,
		  3553, 3562, 3568, 3588, 3594, 3603, 3609, 3620,
		  3626, 3635, 3641, 3666, 3672, 3681, 3687, 3698,
		  3704, 3713, 3719, 3739, 3745, 3754, 3760, 3771,
		  3777, 3786, 3792, 3815, 3821, 3830, 3836, 3847,
		  3853, 3862, 3868, 3888, 3894, 3903, 3909, 3920,
		  3926, 3935, 3941, 3966, 3972, 3981, 3987, 3998,
		  4004, 4013, 4019, 4039, 4045, 4054, 4060, 4071,
		  4077, 4086, 4092, 3956, 3962, 3971, 3977, 3988,
		  3994, 4003, 4009, 4029, 4035, 4044, 4050, 4061,
		  4067, 4076, 4082, 4107, 4113, 4122, 4128, 4139,
		  4145, 4154, 4160, 4180, 4186, 4195, 4201, 4212,
		  4218, 4227, 4233, 4256, 4262, 4271, 4277, 4288,
		  4294, 4303, 4309, 4329, 4335, 4344, 4350, 4361,
		  4367, 4376, 4382, 4407, 4413, 4422, 4428, 4439,
		  4445, 4454, 4460, 4480, 4486, 4495, 4501, 4512,
		  4518, 4527, 4533, 4328, 4334, 4343, 4349, 4360,
		  4366, 4375, 4381, 4401, 4407, 4416, 4422, 4433,
		  4439, 4448, 4454, 4479, 4485, 4494, 4500, 4511,
		  4517, 4526, 4532, 4552, 4558, 4567, 4573, 4584,
		  4590, 4599, 4605, 4628, 4634, 4643, 4649, 4660,
		  4666, 4675, 4681, 4701, 4707, 4716, 4722, 4733,
		  4739, 4748, 4754, 4779, 4785, 4794, 4800, 4811,
		  4817, 4826, 4832, 4852, 4858, 4867, 4873, 4884,
		  4890, 4899, 4905, 4769, 4775, 4784, 4790, 4801,
		  4807, 4816, 4822, 4842, 4848, 4857, 4863, 4874,
		  4880, 4889, 4895, 4920, 4926, 4935, 4941, 4952,
		  4958, 4967, 4973, 4993, 4999, 5008, 5014, 5025,
		  5031, 5040, 5046, 5069, 5075, 5084, 5090, 5101,
		  5107, 5116, 5122, 5142, 5148, 5157, 5163, 5174,
		  5180, 5189, 5195, 5220, 5226, 5235, 5241, 5252,
		  5258, 5267, 5273, 5293, 5299, 5308, 5314, 5325,
		  5331, 5340, 5346, 4604, 4610, 4619, 4625, 4636,
		  4642, 4651, 4657, 4677, 4683, 4692, 4698, 4709,
		  4715, 4724, 4730, 4755, 4761, 4770, 4776, 4787,
		  4793, 4802, 4808, 4828, 4834, 4843, 4849, 4860,
		  4866, 4875, 4881, 4904, 4910, 4919, 4925, 4936,
		  4942, 4951, 4957, 4977, 4983, 4992, 4998, 5009,
		  5015, 5024, 5030, 5055, 5061, 5070, 5076, 5087,
		  5093, 5102, 5108, 5128, 5134, 5143, 5149, 5160,
		  5166, 5175, 5181, 5045, 5051, 5060, 5066, 5077,
		  5083, 5092, 5098, 5118, 5124, 5133, 5139, 5150,
		  5156, 5165, 5171, 5196, 5202, 5211, 5217, 5228,
		  5234, 5243, 5249, 5269, 5275, 5284, 5290, 5301,
		  5307, 5316, 5322, 5345, 5351, 5360, 5366, 5377,
		  5383, 5392, 5398, 5418, 5424, 5433, 5439, 5450,
		  5456, 5465, 5471, 5496, 5502, 5511, 5517, 5528,
		  5534, 5543, 5549, 5569, 5575, 5584, 5590, 5601,
		  5607, 5616, 5622, 5417, 5423, 5432, 5438, 5449,
		  5455, 5464, 5470, 5490, 5496, 5505, 5511, 5522,
		  5528, 5537, 5543, 5568, 5574, 5583, 5589, 5600,
		  5606, 5615, 5621, 5641, 5647, 5656, 5662, 5673,
		  5679, 5688, 5694, 5717, 5723, 5732, 5738, 5749,
		  5755, 5764, 5770, 5790, 5796, 5805, 5811, 5822,
		  5828, 5837, 5843, 5868, 5874, 5883, 5889, 5900,
		  5906, 5915, 5921, 5941, 5947, 5956, 5962, 5973,
		  5979, 5988, 5994, 5858, 5864, 5873, 5879, 5890,
		  5896, 5905, 5911, 5931, 5937, 5946, 5952, 5963,
		  5969, 5978, 5984, 6009, 6015, 6024, 6030, 6041,
		  6047, 6056, 6062, 6082, 6088, 6097, 6103, 6114,
		  6120, 6129, 6135, 6158, 6164, 6173, 6179, 6190,
		  6196, 6205, 6211, 6231, 6237, 6246, 6252, 6263,
		  6269, 6278, 6284, 6309, 6315, 6324, 6330, 6341,
		  6347, 6356, 6362, 6382, 6388, 6397, 6403, 6414,
		  6420, 6429, 6435, 3515, 3521, 3530, 3536, 3547,
		  3553, 3562, 3568, 3588, 3594, 3603, 3609, 3620,
		  3626, 3635, 3641, 3666, 3672, 3681, 3687, 3698,
		  3704, 3713, 3719, 3739, 3745, 3754, 3760, 3771,
		  3777, 3786, 3792, 3815, 3821, 3830, 3836, 3847,
		  3853, 3862, 3868, 3888, 3894, 3903, 3909, 3920,
		  3926, 3935, 3941, 3966, 3972, 3981, 3987, 3998,
		  4004, 4013, 4019, 4039, 4045, 4054, 4060, 4071,
		  4077, 4086, 4092, 3956, 3962, 3971, 3977, 3988,
		  3994, 4003, 4009, 4029, 4035, 4044, 4050, 4061,
		  4067, 4076, 4082, 4107, 4113, 4122, 4128, 4139,
		  4145, 4154, 4160, 4180, 4186, 4195, 4201, 4212,
		  4218, 4227, 4233, 4256, 4262, 4271, 4277, 4288,
		  4294, 4303, 4309, 4329, 4335, 4344, 4350, 4361,
		  4367, 4376, 4382, 4407, 4413, 4422, 4428, 4439,
		  4445, 4454, 4460, 4480, 4486, 4495, 4501, 4512,
		  4518, 4527, 4533, 4328, 4334, 4343, 4349, 4360,
		  4366, 4375, 4381, 4401, 4407, 4416, 4422, 4433,
		  4439, 4448, 4454, 4479, 4485, 4494, 4500, 4511,
		  4517, 4526, 4532, 4552, 4558, 4567, 4573, 4584,
		  4590, 4599, 4605, 4628, 4634, 4643, 4649, 4660,
		  4666, 4675, 4681, 4701, 4707, 4716, 4722, 4733,
		  4739, 4748, 4754, 4779, 4785, 4794, 4800, 4811,
		  4817, 4826, 4832, 4852, 4858, 4867, 4873, 4884,
		  4890, 4899, 4905, 4769, 4775, 4784, 4790, 4801,
		  4807, 4816, 4822, 4842, 4848, 4857, 4863, 4874,
		  4880, 4889, 4895, 4920, 4926, 4935, 4941, 4952,
		  4958, 4967, 4973, 4993, 4999, 5008, 5014, 5025,
		  5031, 5040, 5046, 5069, 5075, 5084, 5090, 5101,
		  5107, 5116, 5122, 5142, 5148, 5157, 5163, 5174,
		  5180, 5189, 5195, 5220, 5226, 5235, 5241, 5252,
		  5258, 5267, 5273, 5293, 5299, 5308, 5314, 5325,
		  5331, 5340, 5346, 4604, 4610, 4619, 4625, 4636,
		  4642, 4651, 4657, 4677, 4683, 4692, 4698, 4709,
		  4715, 4724, 4730, 4755, 4761, 4770, 4776, 4787,
		  4793, 4802, 4808, 4828, 4834, 4843, 4849, 4860,
		  4866, 4875, 4881, 4904, 4910, 4919, 4925, 4936,
		  4942, 4951, 4957, 4977, 4983, 4992, 4998, 5009,
		  5015, 5024, 5030, 5055, 5061, 5070, 5076, 5087,
		  5093, 5102, 5108, 5128, 5134, 5143, 5149, 5160,
		  5166, 5175, 5181, 5045, 5051, 5060, 5066, 5077,
		  5083, 5092, 5098, 5118, 5124, 5133, 5139, 5150,
		  5156, 5165, 5171, 5196, 5202, 5211, 5217, 5228,
		  5234, 5243, 5249, 5269, 5275, 5284, 5290, 5301,
		  5307, 5316, 5322, 5345, 5351, 5360, 5366, 5377,
		  5383, 5392, 5398, 5418, 5424, 5433, 5439, 5450,
		  5456, 5465, 5471, 5496, 5502, 5511, 5517, 5528,
		  5534, 5543, 5549, 5569, 5575, 5584, 5590, 5601,
		  5607, 5616, 5622, 5417, 5423, 5432, 5438, 5449,
		  5455, 5464, 5470, 5490, 5496, 5505, 5511, 5522,
		  5528, 5537, 5543, 5568, 5574, 5583, 5589, 5600,
		  5606, 5615, 5621, 5641, 5647, 5656, 5662, 5673,
		  5679, 5688, 5694, 5717, 5723, 5732, 5738, 5749,
		  5755, 5764, 5770, 5790, 5796, 5805, 5811, 5822,
		  5828, 5837, 5843, 5868, 5874, 5883, 5889, 5900,
		  5906, 5915, 5921, 5941, 5947, 5956, 5962, 5973,
		  5979, 5988, 5994, 5858, 5864, 5873, 5879, 5890,
		  5896, 5905, 5911, 5931, 5937, 5946, 5952, 5963,
		  5969, 5978, 5984, 6009, 6015, 6024, 6030, 6041,
		  6047, 6056, 6062, 6082, 6088, 6097, 6103, 6114,
		  6120, 6129, 6135, 6158, 6164, 6173, 6179, 6190,
		  6196, 6205, 6211, 6231, 6237, 6246, 6252, 6263,
		  6269, 6278, 6284, 6309, 6315, 6324, 6330, 6341,
		  6347, 6356, 6362, 6382, 6388, 6397, 6403, 6414,
		  6420, 6429, 6435, 5303, 5309, 5318, 5324, 5335,
		  5341, 5350, 5356, 5376, 5382, 5391, 5397, 5408,
		  5414, 5423, 5429, 5454, 5460, 5469, 5475, 5486,
		  5492, 5501, 5507, 5527, 5533, 5542, 5548, 5559,
		  5565, 5574, 5580, 5603, 5609, 5618, 5624, 5635,
		  5641, 5650, 5656, 5676, 5682, 5691, 5697, 5708,
		  5714, 5723, 5729, 5754, 5760, 5769, 5775, 5786,
		  5792, 5801, 5807, 5827, 5833, 5842, 5848, 5859,
		  5865, 5874, 5880, 5744, 5750, 5759, 5765, 5776,
		  5782, 5791, 5797, 5817, 5823, 5832, 5838, 5849,
		  5855, 5864, 5870, 5895, 5901, 5910, 5916, 5927,
		  5933, 5942, 5948, 5968, 5974, 5983, 5989, 6000,
		  6006, 6015, 6021, 6044, 6050, 6059, 6065, 6076,
		  6082, 6091, 6097, 6117, 6123, 6132, 6138, 6149,
		  6155, 6164, 6170, 6195, 6201, 6210, 6216, 6227,
		  6233, 6242, 6248, 6268, 6274, 6283, 6289, 6300,
		  6306, 6315, 6321, 6116, 6122, 6131, 6137, 6148,
		  6154, 6163, 6169, 6189, 6195, 6204, 6210, 6221,
		  6227, 6236, 6242, 6267, 6273, 6282, 6288, 6299,
		  6305, 6314, 6320, 6340, 6346, 6355, 6361, 6372,
		  6378, 6387, 6393, 6416, 6422, 6431, 6437, 6448,
		  6454, 6463, 6469, 6489, 6495, 6504, 6510, 6521,
		  6527, 6536, 6542, 6567, 6573, 6582, 6588, 6599,
		  6605, 6614, 6620, 6640, 6646, 6655, 6661, 6672,
		  6678, 6687, 6693, 6557, 6563, 6572, 6578, 6589,
		  6595, 6604, 6610, 6630, 6636, 6645, 6651, 6662,
		  6668, 6677, 6683, 6708, 6714, 6723, 6729, 6740,
		  6746, 6755, 6761, 6781, 6787, 6796, 6802, 6813,
		  6819, 6828, 6834, 6857, 6863, 6872, 6878, 6889,
		  6895, 6904, 6910, 6930, 6936, 6945, 6951, 6962,
		  6968, 6977, 6983, 7008, 7014, 7023, 7029, 7040,
		  7046, 7055, 7061, 7081, 7087, 7096, 7102, 7113,
		  7119, 7128, 7134, 6392, 6398, 6407, 6413, 6424,
		  6430, 6439, 6445, 6465, 6471, 6480, 6486, 6497,
		  6503, 6512, 6518, 6543, 6549, 6558, 6564, 6575,
		  6581, 6590, 6596, 6616, 6622, 6631, 6637, 6648,
		  6654, 6663, 6669, 6692, 6698, 6707, 6713, 6724,
		  6730, 6739, 6745, 6765, 6771, 6780, 6786, 6797,
		  6803, 6812, 6818, 6843, 6849, 6858, 6864, 6875,
		  6881, 6890, 6896, 6916, 6922, 6931, 6937, 6948,
		  6954, 6963, 6969, 6833, 6839, 6848, 6854, 6865,
		  6871, 6880, 6886, 6906, 6912, 6921, 6927, 6938,
		  6944, 6953, 6959, 6984, 6990, 6999, 7005, 7016,
		  7022, 7031, 7037, 7057, 7063, 7072, 7078, 7089,
		  7095, 7104, 7110, 7133, 7139, 7148, 7154, 7165,
		  7171, 7180, 7186, 7206, 7212, 7221, 7227, 7238,
		  7244, 7253, 7259, 7284, 7290, 7299, 7305, 7316,
		  7322, 7331, 7337, 7357, 7363, 7372, 7378, 7389,
		  7395, 7404, 7410, 7205, 7211, 7220, 7226, 7237,
		  7243, 7252, 7258, 7278, 7284, 7293, 7299, 7310,
		  7316, 7325, 7331, 7356, 7362, 7371, 7377, 7388,
		  7394, 7403, 7409, 7429, 7435, 7444, 7450, 7461,
		  7467, 7476, 7482, 7505, 7511, 7520, 7526, 7537,
		  7543, 7552, 7558, 7578, 7584, 7593, 7599, 7610,
		  7616, 7625, 7631, 7656, 7662, 7671, 7677, 7688,
		  7694, 7703, 7709, 7729, 7735, 7744, 7750, 7761
		};

		static int VariableLevelCost(int level, const byte probas[NUM_PROBAS]) {
		  int pattern = VP8LevelCodes[level - 1][0];
		  int bits = VP8LevelCodes[level - 1][1];
		  int cost = 0;
		  int i;
		  for (i = 2; pattern; ++i) {
			if (pattern & 1) {
			  cost += VP8BitCost(bits & 1, probas[i]);
			}
			bits >>= 1;
			pattern >>= 1;
		  }
		  return cost;
		}

		//------------------------------------------------------------------------------
		// Pre-calc level costs once for all

		void VP8CalculateLevelCosts(VP8Proba* const proba) {
		  int ctype, band, ctx;

		  if (!proba->dirty_) return;  // nothing to do.

		  for (ctype = 0; ctype < NUM_TYPES; ++ctype) {
			for (band = 0; band < NUM_BANDS; ++band) {
			  for(ctx = 0; ctx < NUM_CTX; ++ctx) {
				const byte* const p = proba->coeffs_[ctype][band][ctx];
				ushort* const table = proba->level_cost_[ctype][band][ctx];
				const int cost_base = VP8BitCost(1, p[1]);
				int v;
				table[0] = VP8BitCost(0, p[1]);
				for (v = 1; v <= MAX_VARIABLE_LEVEL; ++v) {
				  table[v] = cost_base + VariableLevelCost(v, p);
				}
				// Starting at level 67 and up, the variable part of the cost is
				// actually constant.
			  }
			}
		  }
		  proba->dirty_ = 0;
		}

		//------------------------------------------------------------------------------
		// Mode cost tables.

		// These are the fixed probabilities (in the coding trees) turned into bit-cost
		// by calling VP8BitCost().
		const ushort VP8FixedCostsUV[4] = { 302, 984, 439, 642 };
		// note: these values include the fixed VP8BitCost(1, 145) mode selection cost.
		const ushort VP8FixedCostsI16[4] = { 663, 919, 872, 919 };
		const ushort VP8FixedCostsI4[NUM_BMODES][NUM_BMODES][NUM_BMODES] = {
		  { {  251, 1362, 1934, 2085, 2314, 2230, 1839, 1988, 2437, 2348 },
			{  403,  680, 1507, 1519, 2060, 2005, 1992, 1914, 1924, 1733 },
			{  353, 1121,  973, 1895, 2060, 1787, 1671, 1516, 2012, 1868 },
			{  770,  852, 1581,  632, 1393, 1780, 1823, 1936, 1074, 1218 },
			{  510, 1270, 1467, 1319,  847, 1279, 1792, 2094, 1080, 1353 },
			{  488, 1322,  918, 1573, 1300,  883, 1814, 1752, 1756, 1502 },
			{  425,  992, 1820, 1514, 1843, 2440,  937, 1771, 1924, 1129 },
			{  363, 1248, 1257, 1970, 2194, 2385, 1569,  953, 1951, 1601 },
			{  723, 1257, 1631,  964,  963, 1508, 1697, 1824,  671, 1418 },
			{  635, 1038, 1573,  930, 1673, 1413, 1410, 1687, 1410,  749 } },
		  { {  451,  613, 1345, 1702, 1870, 1716, 1728, 1766, 2190, 2310 },
			{  678,  453, 1171, 1443, 1925, 1831, 2045, 1781, 1887, 1602 },
			{  711,  666,  674, 1718, 1910, 1493, 1775, 1193, 2325, 2325 },
			{  883,  854, 1583,  542, 1800, 1878, 1664, 2149, 1207, 1087 },
			{  669,  994, 1248, 1122,  949, 1179, 1376, 1729, 1070, 1244 },
			{  715, 1026,  715, 1350, 1430,  930, 1717, 1296, 1479, 1479 },
			{  544,  841, 1656, 1450, 2094, 3883, 1010, 1759, 2076,  809 },
			{  610,  855,  957, 1553, 2067, 1561, 1704,  824, 2066, 1226 },
			{  833,  960, 1416,  819, 1277, 1619, 1501, 1617,  757, 1182 },
			{  711,  964, 1252,  879, 1441, 1828, 1508, 1636, 1594,  734 } },
		  { {  605,  764,  734, 1713, 1747, 1192, 1819, 1353, 1877, 2392 },
			{  866,  641,  586, 1622, 2072, 1431, 1888, 1346, 2189, 1764 },
			{  901,  851,  456, 2165, 2281, 1405, 1739, 1193, 2183, 2443 },
			{  770, 1045,  952, 1078, 1342, 1191, 1436, 1063, 1303,  995 },
			{  901, 1086,  727, 1170,  884, 1105, 1267, 1401, 1739, 1337 },
			{  951, 1162,  595, 1488, 1388,  703, 1790, 1366, 2057, 1724 },
			{  534,  986, 1273, 1987, 3273, 1485, 1024, 1399, 1583,  866 },
			{  699, 1182,  695, 1978, 1726, 1986, 1326,  714, 1750, 1672 },
			{  951, 1217, 1209,  920, 1062, 1441, 1548,  999,  952,  932 },
			{  733, 1284,  784, 1256, 1557, 1098, 1257, 1357, 1414,  908 } },
		  { {  316, 1075, 1653, 1220, 2145, 2051, 1730, 2131, 1884, 1790 },
			{  745,  516, 1404,  894, 1599, 2375, 2013, 2105, 1475, 1381 },
			{  516,  729, 1088, 1319, 1637, 3426, 1636, 1275, 1531, 1453 },
			{  894,  943, 2138,  468, 1704, 2259, 2069, 1763, 1266, 1158 },
			{  605, 1025, 1235,  871, 1170, 1767, 1493, 1500, 1104, 1258 },
			{  739,  826, 1207, 1151, 1412,  846, 1305, 2726, 1014, 1569 },
			{  558,  825, 1820, 1398, 3344, 1556, 1218, 1550, 1228,  878 },
			{  429,  951, 1089, 1816, 3861, 3861, 1556,  969, 1568, 1828 },
			{  883,  961, 1752,  769, 1468, 1810, 2081, 2346,  613, 1298 },
			{  803,  895, 1372,  641, 1303, 1708, 1686, 1700, 1306, 1033 } },
		  { {  439, 1267, 1270, 1579,  963, 1193, 1723, 1729, 1198, 1993 },
			{  705,  725, 1029, 1153, 1176, 1103, 1821, 1567, 1259, 1574 },
			{  723,  859,  802, 1253,  972, 1202, 1407, 1665, 1520, 1674 },
			{  894,  960, 1254,  887, 1052, 1607, 1344, 1349,  865, 1150 },
			{  833, 1312, 1337, 1205,  572, 1288, 1414, 1529, 1088, 1430 },
			{  842, 1279, 1068, 1861,  862,  688, 1861, 1630, 1039, 1381 },
			{  766,  938, 1279, 1546, 3338, 1550, 1031, 1542, 1288,  640 },
			{  715, 1090,  835, 1609, 1100, 1100, 1603, 1019, 1102, 1617 },
			{  894, 1813, 1500, 1188,  789, 1194, 1491, 1919,  617, 1333 },
			{  610, 1076, 1644, 1281, 1283,  975, 1179, 1688, 1434,  889 } },
		  { {  544,  971, 1146, 1849, 1221,  740, 1857, 1621, 1683, 2430 },
			{  723,  705,  961, 1371, 1426,  821, 2081, 2079, 1839, 1380 },
			{  783,  857,  703, 2145, 1419,  814, 1791, 1310, 1609, 2206 },
			{  997, 1000, 1153,  792, 1229, 1162, 1810, 1418,  942,  979 },
			{  901, 1226,  883, 1289,  793,  715, 1904, 1649, 1319, 3108 },
			{  979, 1478,  782, 2216, 1454,  455, 3092, 1591, 1997, 1664 },
			{  663, 1110, 1504, 1114, 1522, 3311,  676, 1522, 1530, 1024 },
			{  605, 1138, 1153, 1314, 1569, 1315, 1157,  804, 1574, 1320 },
			{  770, 1216, 1218, 1227,  869, 1384, 1232, 1375,  834, 1239 },
			{  775, 1007,  843, 1216, 1225, 1074, 2527, 1479, 1149,  975 } },
		  { {  477,  817, 1309, 1439, 1708, 1454, 1159, 1241, 1945, 1672 },
			{  577,  796, 1112, 1271, 1618, 1458, 1087, 1345, 1831, 1265 },
			{  663,  776,  753, 1940, 1690, 1690, 1227, 1097, 3149, 1361 },
			{  766, 1299, 1744, 1161, 1565, 1106, 1045, 1230, 1232,  707 },
			{  915, 1026, 1404, 1182, 1184,  851, 1428, 2425, 1043,  789 },
			{  883, 1456,  790, 1082, 1086,  985, 1083, 1484, 1238, 1160 },
			{  507, 1345, 2261, 1995, 1847, 3636,  653, 1761, 2287,  933 },
			{  553, 1193, 1470, 2057, 2059, 2059,  833,  779, 2058, 1263 },
			{  766, 1275, 1515, 1039,  957, 1554, 1286, 1540, 1289,  705 },
			{  499, 1378, 1496, 1385, 1850, 1850, 1044, 2465, 1515,  720 } },
		  { {  553,  930,  978, 2077, 1968, 1481, 1457,  761, 1957, 2362 },
			{  694,  864,  905, 1720, 1670, 1621, 1429,  718, 2125, 1477 },
			{  699,  968,  658, 3190, 2024, 1479, 1865,  750, 2060, 2320 },
			{  733, 1308, 1296, 1062, 1576, 1322, 1062, 1112, 1172,  816 },
			{  920,  927, 1052,  939,  947, 1156, 1152, 1073, 3056, 1268 },
			{  723, 1534,  711, 1547, 1294,  892, 1553,  928, 1815, 1561 },
			{  663, 1366, 1583, 2111, 1712, 3501,  522, 1155, 2130, 1133 },
			{  614, 1731, 1188, 2343, 1944, 3733, 1287,  487, 3546, 1758 },
			{  770, 1585, 1312,  826,  884, 2673, 1185, 1006, 1195, 1195 },
			{  758, 1333, 1273, 1023, 1621, 1162, 1351,  833, 1479,  862 } },
		  { {  376, 1193, 1446, 1149, 1545, 1577, 1870, 1789, 1175, 1823 },
			{  803,  633, 1136, 1058, 1350, 1323, 1598, 2247, 1072, 1252 },
			{  614, 1048,  943,  981, 1152, 1869, 1461, 1020, 1618, 1618 },
			{ 1107, 1085, 1282,  592, 1779, 1933, 1648, 2403,  691, 1246 },
			{  851, 1309, 1223, 1243,  895, 1593, 1792, 2317,  627, 1076 },
			{  770, 1216, 1030, 1125,  921,  981, 1629, 1131, 1049, 1646 },
			{  626, 1469, 1456, 1081, 1489, 3278,  981, 1232, 1498,  733 },
			{  617, 1201,  812, 1220, 1476, 1476, 1478,  970, 1228, 1488 },
			{ 1179, 1393, 1540,  999, 1243, 1503, 1916, 1925,  414, 1614 },
			{  943, 1088, 1490,  682, 1112, 1372, 1756, 1505,  966,  966 } },
		  { {  322, 1142, 1589, 1396, 2144, 1859, 1359, 1925, 2084, 1518 },
			{  617,  625, 1241, 1234, 2121, 1615, 1524, 1858, 1720, 1004 },
			{  553,  851,  786, 1299, 1452, 1560, 1372, 1561, 1967, 1713 },
			{  770,  977, 1396,  568, 1893, 1639, 1540, 2108, 1430, 1013 },
			{  684, 1120, 1375,  982,  930, 2719, 1638, 1643,  933,  993 },
			{  553, 1103,  996, 1356, 1361, 1005, 1507, 1761, 1184, 1268 },
			{  419, 1247, 1537, 1554, 1817, 3606, 1026, 1666, 1829,  923 },
			{  439, 1139, 1101, 1257, 3710, 1922, 1205, 1040, 1931, 1529 },
			{  979,  935, 1269,  847, 1202, 1286, 1530, 1535,  827, 1036 },
			{  516, 1378, 1569, 1110, 1798, 1798, 1198, 2199, 1543,  712 } },
		};

		//------------------------------------------------------------------------------


	}
}