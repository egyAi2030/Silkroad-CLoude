#include "debug.h"
#include <stdio.h>
#include <stdarg.h>


#include <unordered_map>


int m_group = DEBUG_FILE_GEN;
FILE *dbgfile;


int lines = 0;

int debug(debug_group group, const char *format, ...) {
	va_list vl;
	char buffer[512];

	// Filter out unwanted debugging messages
	if (!(group & m_group)) {
		return 0;
	}
	
	va_start(vl, format);

	sprintf_s(buffer, sizeof(buffer), "%s\n", format);

	vprintf(buffer, vl);
	vfprintf(dbgfile, buffer, vl);

	if (++lines > 2) {
		fflush(dbgfile);
	}

	va_end(vl);

	return 0;
}
