#!/usr/bin/env python3
"""Import PNGs from the artist's RAR; preserve unchanged files and Unity GUIDs."""
import argparse, ctypes as C, ctypes.util, hashlib, json, struct
from pathlib import Path

def images(path):
    lib=C.CDLL(ctypes.util.find_library('archive'));lib.archive_read_new.restype=C.c_void_p
    for name in ['archive_read_support_format_all','archive_read_support_filter_all','archive_read_free','archive_read_data_skip']:
        getattr(lib,name).argtypes=[C.c_void_p]
    lib.archive_read_open_filename.argtypes=[C.c_void_p,C.c_char_p,C.c_size_t]
    lib.archive_read_next_header.argtypes=[C.c_void_p,C.POINTER(C.c_void_p)]
    lib.archive_entry_pathname.argtypes=[C.c_void_p];lib.archive_entry_pathname.restype=C.c_char_p
    lib.archive_read_data.argtypes=[C.c_void_p,C.c_void_p,C.c_size_t];lib.archive_read_data.restype=C.c_ssize_t
    handle=lib.archive_read_new();lib.archive_read_support_filter_all(handle);lib.archive_read_support_format_all(handle)
    try:
        if lib.archive_read_open_filename(handle,str(path).encode(),65536)!=0:raise ValueError('Cannot open archive')
        entry=C.c_void_p();buffer=C.create_string_buffer(65536)
        while True:
            status=lib.archive_read_next_header(handle,C.byref(entry))
            if status==1:break
            if status<0:raise ValueError('Invalid RAR entry')
            name=lib.archive_entry_pathname(entry).decode()
            if not name.endswith('.png'):lib.archive_read_data_skip(handle);continue
            chunks=[]
            while True:
                size=lib.archive_read_data(handle,buffer,len(buffer))
                if size<0:raise ValueError('Cannot read '+name)
                if not size:break
                chunks.append(buffer.raw[:size])
            yield name,b''.join(chunks)
    finally:lib.archive_read_free(handle)

def main():
    p=argparse.ArgumentParser();p.add_argument('archive',type=Path);args=p.parse_args();records=[];same=0
    for source,data in images(args.archive):
        rel=source.removeprefix('New_Sprites/');size=struct.unpack('>II',data[16:24])
        if rel=='Mine_LandscapeFULLSCR.png':continue # Exact nearest-neighbour 5x duplicate.
        if rel=='Mine_Landscape.png':dest=Path('Assets/PickaxeFall/Resources/Backgrounds/MineLandscape.png')
        elif rel.startswith('Structures/Tnt/'):dest=Path('Assets/PickaxeFall/Resources/Blocks/Tnt')/Path(rel).name
        elif rel.startswith('Structures/Workbench/'):dest=Path('Assets/PickaxeFall/Resources/Blocks/Workbench')/Path(rel).name
        elif rel.startswith(('Desert/','Mine/','Nether/','Structures/')) and '..' not in Path(rel).parts:dest=Path('Assets/PickaxeFall/Resources/Blocks/World')/rel
        else:raise ValueError('Unmapped sprite '+source)
        if dest.exists() and dest.read_bytes()==data:same+=1;continue
        dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
        records.append({'source':source,'destination':str(dest),'width':size[0],'height':size[1],'sha256':hashlib.sha256(data).hexdigest()})
    if records:Path('Docs/SpriteSources084.json').write_text(json.dumps({'archive':args.archive.name,'entries':records},indent=2)+'\n')
    print('Imported',len(records),'PNGs; preserved',same,'identical files')

if __name__=='__main__':main()
