#pragma once

#include "engine/lumix.h"

namespace Lumix
{
namespace FS
{


struct IFile;


struct LUMIX_ENGINE_API IFileDevice
{
	IFileDevice() {}
	virtual ~IFileDevice() {}

	virtual IFile* createFile(IFile* child) = 0;
	virtual void destroyFile(IFile* file) = 0;

	virtual const char* name() const = 0;
};


} // namespace FS
} // namespace Lumix
