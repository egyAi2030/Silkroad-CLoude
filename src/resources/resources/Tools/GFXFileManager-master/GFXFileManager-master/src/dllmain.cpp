#include <Windows.h>
#include <stdio.h>

//#include "CPFileManager.h"
//#include "CWFileManager.h"
#include "debug.h"

HMODULE hInstance;

extern FILE *dbgfile;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ulReason, LPVOID lpReserved) {

	UNREFERENCED_PARAMETER(lpReserved); // Suppress warning, lol

	if (ulReason == DLL_PROCESS_ATTACH) {
		hInstance = hModule;

		AllocConsole();
		freopen("CONOUT$", "w", stdout);

		dbgfile = fopen("gfxlog.txt", "w");

		DisableThreadLibraryCalls(hModule);
	} else if (ulReason == DLL_PROCESS_DETACH) {

		fclose(dbgfile);

	}

	return TRUE;
}
