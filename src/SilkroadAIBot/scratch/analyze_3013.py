import struct

def parse_3013(hex_str):
    data = bytes.fromhex(hex_str)
    offset = 0
    
    def read_u8():
        nonlocal offset
        val = data[offset]
        offset += 1
        return val
    
    def read_u16():
        nonlocal offset
        val = struct.unpack('<H', data[offset:offset+2])[0]
        offset += 2
        return val
        
    def read_u32():
        nonlocal offset
        val = struct.unpack('<I', data[offset:offset+4])[0]
        offset += 4
        return val
        
    def read_u64():
        nonlocal offset
        val = struct.unpack('<Q', data[offset:offset+8])[0]
        offset += 8
        return val

    print(f"Total Size: {len(data)}")
    
    # Standard vSRO Structure
    try:
        server_time = read_u32()
        model_id = read_u32()
        scale = read_u8()
        level = read_u8()
        max_level = read_u8()
        exp = read_u64()
        sexp = read_u32()
        gold = read_u64()
        sp = read_u32()
        stat_points = read_u16()
        berserk = read_u8()
        gained_exp = read_u32() # or similar u64/u32
        
        hp = read_u32()
        mp = read_u32()
        
        print(f"Server Time: {server_time}")
        print(f"Model ID: {model_id}")
        print(f"Scale: {scale}")
        print(f"Level: {level}")
        print(f"Max Level: {max_level}")
        print(f"Exp: {exp}")
        print(f"SExp: {sexp}")
        print(f"Gold: {gold}")
        print(f"SP: {sp}")
        print(f"Stat Points: {stat_points}")
        print(f"Berserk: {berserk}")
        print(f"HP: {hp}")
        print(f"MP: {mp}")
        
        # Next byte is usually DailyPK (1) or similar
        print(f"Next bytes (hex): {data[offset:offset+10].hex(' ')}")
        
    except Exception as e:
        print(f"Error during parsing: {e} at offset {offset}")

# Raw data from line 295-298
raw_hex = "1A61A19B73070000226E6EF2466D8FA400000000FD000000323F080700000000F55C7300000000000000000000D27000001ADF0000000000000000000022006D3300000000009C1200000665A10002"
parse_3013(raw_hex)
