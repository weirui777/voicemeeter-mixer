import ctypes, time, sys

vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
vm.VBVMR_Login.restype = ctypes.c_long
vm.VBVMR_Logout.restype = ctypes.c_long
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_SetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.c_float]
vm.VBVMR_IsParametersDirty.restype = ctypes.c_long


def getf(n):
    v = ctypes.c_float()
    rc = vm.VBVMR_GetParameterFloat(n.encode(), ctypes.byref(v))
    return ("%.3f" % v.value) if rc == 0 else ("ERR%d" % rc)


mode = sys.argv[1] if len(sys.argv) > 1 else "writer"

if mode == "reader":
    # foreign client: login, read, logout
    time.sleep(2.0)
    print("[reader] login rc=", vm.VBVMR_Login())
    print("[reader] read ->", getf("Bus[1].Gain"))
    vm.VBVMR_Logout()
    print("[reader] done")
else:
    # writer: holds the session and pushes values repeatedly
    print("[writer] login rc=", vm.VBVMR_Login())
    print("[writer] start value:", getf("Bus[1].Gain"))
    val = -21.0
    for i in range(16):
        vm.VBVMR_SetParameterFloat(b"Bus[1].Gain", ctypes.c_float(val))
        for _ in range(8):
            vm.VBVMR_IsParametersDirty()
            time.sleep(0.01)
        print("[writer] t=%.2f pushed %.1f readback %s" % (i * 0.4, val, getf("Bus[1].Gain")))
        time.sleep(0.32)
        val += 1.0
    # restore
    for _ in range(20):
        vm.VBVMR_SetParameterFloat(b"Bus[1].Gain", ctypes.c_float(-3.19))
        vm.VBVMR_IsParametersDirty()
        time.sleep(0.01)
    print("[writer] restored ->", getf("Bus[1].Gain"))
    vm.VBVMR_Logout()
