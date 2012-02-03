using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal
{
	class cpu
	{
		//------------------------------------------------------------------------------
		// SSE2 detection.
		//

		// apple/darwin gcc-4.0.1 defines __PIC__, but not __pic__ with -fPIC.
		#if (defined(__pic__) || defined(__PIC__)) && defined(__i386__)
		static void GetCPUInfo(int cpu_info[4], int info_type) {
		  __asm__ volatile (
			"mov %%ebx, %%edi\n"
			"cpuid\n"
			"xchg %%edi, %%ebx\n"
			: "=a"(cpu_info[0]), "=D"(cpu_info[1]), "=c"(cpu_info[2]), "=d"(cpu_info[3])
			: "a"(info_type));
		}
		#elif defined(__i386__) || defined(__x86_64__)
		static void GetCPUInfo(int cpu_info[4], int info_type) {
		  __asm__ volatile (
			"cpuid\n"
			: "=a"(cpu_info[0]), "=b"(cpu_info[1]), "=c"(cpu_info[2]), "=d"(cpu_info[3])
			: "a"(info_type));
		}
		#elif defined(WEBP_MSC_SSE2)
		#define GetCPUInfo __cpuid
		#endif

		#if defined(__i386__) || defined(__x86_64__) || defined(WEBP_MSC_SSE2)
		static int x86CPUInfo(CPUFeature feature) {
		  int cpu_info[4];
		  GetCPUInfo(cpu_info, 1);
		  if (feature == kSSE2) {
			return 0 != (cpu_info[3] & 0x04000000);
		  }
		  if (feature == kSSE3) {
			return 0 != (cpu_info[2] & 0x00000001);
		  }
		  return 0;
		}
		VP8CPUInfo VP8GetCPUInfo = x86CPUInfo;
		#elif defined(__ARM_NEON__)
		// define a dummy function to enable turning off NEON at runtime by setting
		// VP8DecGetCPUInfo = null
		static int armCPUInfo(CPUFeature feature) {
		  (void)feature;
		  return 1;
		}
		VP8CPUInfo VP8GetCPUInfo = armCPUInfo;
		#else
		VP8CPUInfo VP8GetCPUInfo = null;
		#endif

	}
}
