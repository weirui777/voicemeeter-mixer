import ctypes, time

vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
vm.VBVMR_Login.restype = ctypes.c_long
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_IsParametersDirty.restype = ctypes.c_long

NAMES = ["Bus[0].Gain", "Bus[1].Gain", "Strip[0].Gain", "Strip[3].Gain", "Strip[4].Gain"]


def getf(n):
    v = ctypes.c_float()
    rc = vm.VBVMR_GetParameterFloat(n.encode(), ctypes.byref(v))
    return ("%.3f" % v.value) if rc == 0 else ("ERR%d" % rc)


print("login rc=", vm.VBVMR_Login())
print("--- after login ---")
for n in NAMES:
    print("   %-14s %s" % (n, getf(n)))

print("--- pump IsParametersDirty x 10 ---")
for i in range(10):
    r = vm.VBVMR_IsParametersDirty()
    time.sleep(0.02)
print("   (last dirty return = %d)" % r)
for n in NAMES:
    print("   %-14s %s" % (n, getf(n)))

print("--- pump again x 20 ---")
for i in range(20):
    vm.VBVMR_IsParametersDirty()
    time.sleep(0.02)
for n in NAMES:
    print("   %-14s %s" % (n, getf(n)))

vm.VBVMR_Logout()
