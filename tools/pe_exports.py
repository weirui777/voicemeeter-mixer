import struct, sys, os


def exports(path):
    data = open(path, 'rb').read()
    if data[:2] != b'MZ':
        return []
    e_lfanew = struct.unpack_from('<I', data, 0x3C)[0]
    if data[e_lfanew:e_lfanew + 4] != b'PE\0\0':
        return []
    coff = e_lfanew + 4
    nsec, = struct.unpack_from('<H', data, coff + 2)
    opt_size, = struct.unpack_from('<H', data, coff + 16)
    opt = coff + 20
    magic, = struct.unpack_from('<H', data, opt)
    dd = opt + (112 if magic == 0x20B else 96)
    exp_rva, exp_size = struct.unpack_from('<II', data, dd)

    secs = []
    sec_off = opt + opt_size
    for i in range(nsec):
        o = sec_off + i * 40
        name = data[o:o + 8].rstrip(b'\0').decode('latin1')
        vsize, vaddr, rsize, raddr = struct.unpack_from('<IIII', data, o + 8)
        secs.append((vaddr, vsize, raddr, rsize))

    def rva2off(rva):
        for vaddr, vsize, raddr, rsize in secs:
            if vaddr <= rva < vaddr + max(vsize, rsize):
                return raddr + (rva - vaddr)
        return None

    if not exp_rva:
        return []
    eo = rva2off(exp_rva)
    if eo is None:
        return []
    nfun, nname = struct.unpack_from('<II', data, eo + 20)
    addr_rva, name_rva, ord_rva = struct.unpack_from('<III', data, eo + 28)
    names = []
    no = rva2off(name_rva)
    for i in range(nname):
        n_rva, = struct.unpack_from('<I', data, no + i * 4)
        o = rva2off(n_rva)
        if o is None:
            continue
        end = data.index(b'\0', o)
        names.append(data[o:end].decode('latin1'))
    return names


for p in sys.argv[1:]:
    if not os.path.exists(p):
        print('MISSING', p)
        continue
    try:
        ex = exports(p)
    except Exception as e:
        print('ERR', p, e)
        continue
    print('=== %s (%d exports) ===' % (os.path.basename(p), len(ex)))
    for n in ex:
        print('   ', n)
