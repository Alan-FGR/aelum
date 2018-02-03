#pragma once

#include "engine/delegate.h"


namespace Lumix
{

struct IAllocator;

class LUMIX_EDITOR_API FileSystemWatcher
{
	public:
		virtual ~FileSystemWatcher() {}

		static FileSystemWatcher* create(const char* path, IAllocator& allocator);
		static void destroy(FileSystemWatcher* watcher); 
		virtual Delegate<void (const char*)>& getCallback() = 0;
};


} // namespace Lumix