import ctypes, time

vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
vm.VBVMR_Login.restype = ctypes.c_long
vm.VBVMR_Logout.restype = ctypes.c_long
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_SetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.c_float]
vm.VBVMR_GetLevel.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_IsParametersDirty.restype = ctypes.c_long

NAMES = ["Bus[0].Gain", "Bus[1].Gain", "Strip[0].Gain", "Strip[3].Gain", "Strip[4].Gain"]


def getf(n):
    v = ctypes.c_float()
    rc = vm.VBVMR_GetParameterFloat(n.encode(), ctypes.byref(v))
    return ("%.3f" % v.value) if rc == 0 else ("ERR%d" % rc)


def show(tag):
    print("   %-16s %s" % (tag, " | ".join("%s=%s" % (n, getf(n)) for n in NAMES)))


def level(mode, ch):
    v = ctypes.c_float()
    rc = vm.VBVMR_GetLevel(mode, ch, ctypes.byref(v))
    return ("%.3f" % v.value) if rc == 0 else ("ERR%d" % rc)


print("A) fresh login")
vm.VBVMR_Login()
show("after login")

print("B) levels (what the garbage looks like)")
print("   bus0 level(mode3,ch0) =", level(3, 0))
print("   bus1 level(mode3,ch8) =", level(3, 8))
print("   strip3 level(mode1,ch6) =", level(1, 6))
print("   strip4 level(mode1,ch12) =", level(1, 12))

print("C) pump once")
vm.VBVMR_IsParametersDirty()
show("after pump")

print("D) logout + login again")
vm.VBVMR_Logout()
time.sleep(0.2)
vm.VBVMR_Login()
show("after re-login")

print("E) pump once more")
vm.VBVMR_IsParametersDirty()
show("after pump2")

print("F) write a gain and pump, then read")
vm.VBVMR_SetParameterFloat(b"Bus[0].Gain", ctypes.c_float(-9.8))
vm.VBVMR_IsParametersDirty()
time.sleep(0.05)
show("after write+pump")

print("G) read via a *different* fresh session (ground truth)")
vm.VBVMR_Logout()
time.sleep(0.2)
vm.VBVMR_Login()
show("ground truth")
vm.VBVMR_Logout()
