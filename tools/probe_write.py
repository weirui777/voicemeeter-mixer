import ctypes, sys, io, time
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
L = ctypes.c_long
vm.VBVMR_Login.restype = L
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_GetParameterFloat.restype = L
vm.VBVMR_SetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.c_float]
vm.VBVMR_SetParameterFloat.restype = L
vm.VBVMR_IsParametersDirty.restype = L
print("login", vm.VBVMR_Login())


def getf(name):
    v = ctypes.c_float(0)
    r = vm.VBVMR_GetParameterFloat(name.encode(), ctypes.byref(v))
    return r, round(v.value, 3)


def setf(name, val):
    return vm.VBVMR_SetParameterFloat(name.encode(), ctypes.c_float(val))


for target in [("Bus[0].Gain", -12.0), ("Strip[3].Gain", -7.0), ("Bus[0].gain", -11.0)]:
    name, val = target
    before = getf(name)
    rc = setf(name, val)
    time.sleep(0.05)
    after50 = getf(name)
    time.sleep(0.45)
    after500 = getf(name)
    dirty = vm.VBVMR_IsParametersDirty()
    print(f"{name}: before={before} set rc={rc} +50ms={after50} +500ms={after500} dirty={dirty}")

# restore
orig_bus0 = getf("Bus[0].Gain")[1]
orig_strip3 = getf("Strip[3].Gain")[1]
setf("Bus[0].Gain", orig_bus0)
setf("Strip[3].Gain", orig_strip3)
print("restored:", getf("Bus[0].Gain"), getf("Strip[3].Gain"))
vm.VBVMR_Logout()
