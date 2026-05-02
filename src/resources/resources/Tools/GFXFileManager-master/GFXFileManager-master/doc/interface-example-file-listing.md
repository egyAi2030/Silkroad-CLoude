# Example code for file listing

This code will utilize existing GFXFileManager-libraries and make them show the contents of their container.

![cool image](doc/example-file-listing.png)


```cpp
HMODULE gfxfilemanager = LoadLibrary("GFXFileManager.dll");

f_GfxCreateObject* create = (f_GfxCreateObject*)GetProcAddress(gfxfilemanager, "GFXDllCreateObject");

IFileManager* mgr = 0;

create(1, &mgr, 0x1007);


mgr->OpenContainer("Particles.pk2", "169841", 1);

if (mgr->IsOpen()) {
	std::cout << "Container is now open" << std::endl;
}



searchresult_t m_result;
result_entry_t m_find;

mgr->FindFirstFile(&m_result, "*.*", &m_find);

if (m_result.success == 0) {
	std::cout << "No files found" << std::endl;
}


do {
	switch(m_find.type) {

	case ENTRY_FILE:
		std::cout << "F: " << m_find.name << std::endl;
		break;

	case ENTRY_FOLDER:
		std::cout << "D: " << m_find.name << std::endl;
		break;

	default:
		std::cout << 
			"Unknown type " << std::hex << (int)m_find.type << std::endl;
	}
	
	mgr->FindNextFile(&m_result, &m_find);

} while (m_result.success != 0);


mgr->FindClose(&m_result);


```
