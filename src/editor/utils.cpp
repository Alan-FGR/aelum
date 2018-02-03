#include "utils.h"
#include "engine/math_utils.h"
#include "engine/path.h"
#include "engine/path_utils.h"
#include "engine/reflection.h"
#include "editor/render_interface.h"
#include "editor/world_editor.h"
#include "imgui/imgui.h"
#include "engine/universe/universe.h"
#include <SDL.h>


namespace Lumix
{


Action::Action(const char* label_short, const char* label_long, const char* name)
	: label_long(label_long)
	, label_short(label_short)
	, name(name)
	, plugin(nullptr)
	, is_global(true)
	, icon(nullptr)
{
	this->label_short = label_short;
	this->label_long = label_long;
	this->name = name;
	shortcut[0] = shortcut[1] = shortcut[2] = -1;
	is_selected.bind<falseConst>();
}


Action::Action(const char* label_short,
	const char* label_long,
	const char* name,
	int shortcut0,
	int shortcut1,
	int shortcut2)
	: label_long(label_long)
	, label_short(label_short)
	, name(name)
	, plugin(nullptr)
	, is_global(true)
	, icon(nullptr)
{
	shortcut[0] = shortcut0;
	shortcut[1] = shortcut1;
	shortcut[2] = shortcut2;
	is_selected.bind<falseConst>();
}


bool Action::toolbarButton()
{
	if (!icon) return false;

	ImVec4 col_active = ImGui::GetStyle().Colors[ImGuiCol_ButtonActive];
	ImVec4 bg_color = is_selected.invoke() ? col_active : ImVec4(0, 0, 0, 0);
	if (ImGui::ToolbarButton(icon, bg_color, label_long))
	{
		func.invoke();
		return true;
	}
	return false;
}


void Action::getIconPath(char* path, int max_size)
{
	copyString(path, max_size, "models/editor/icon_"); 
		
	char tmp[1024];
	const char* c = name;
	char* out = tmp;
	while (*c)
	{
		if (*c >= 'A' && *c <= 'Z') *out = *c - ('A' - 'a');
		else if (*c >= 'a' && *c <= 'z') *out = *c;
		else *out = '_';
		++out;
		++c;
	}
	*out = 0;

	catString(path, max_size, tmp);
	catString(path, max_size, ".dds");
}


bool Action::isRequested()
{
	if (ImGui::IsAnyItemActive()) return false;

	bool* keys_down = ImGui::GetIO().KeysDown;
	float* keys_down_duration = ImGui::GetIO().KeysDownDuration;
	if (shortcut[0] == -1) return false;

	for (int i = 0; i < lengthOf(shortcut) + 1; ++i)
	{
		if (i == lengthOf(shortcut) || shortcut[i] == -1)
		{
			return true;
		}

		if (!keys_down[shortcut[i]] || keys_down_duration[shortcut[i]] > 0) return false;
	}
	return false;
}



bool Action::isActive()
{
	if (ImGui::IsAnyItemActive()) return false;
	if (shortcut[0] == -1) return false;

	int key_count;
	auto* state = SDL_GetKeyboardState(&key_count);

	for (int i = 0; i < lengthOf(shortcut) + 1; ++i)
	{
		if (i == lengthOf(shortcut) || shortcut[i] == -1)
		{
			return true;
		}
		SDL_Scancode scancode = SDL_GetScancodeFromKey(shortcut[i]);

		if (scancode >= key_count || !state[scancode]) return false;
	}
	return false;
}


void getEntityListDisplayName(WorldEditor& editor, char* buf, int max_size, Entity entity)
{
	if (!entity.isValid())
	{
		*buf = '\0';
		return;
	}
	const char* name = editor.getUniverse()->getEntityName(entity);
	static const auto MODEL_INSTANCE_TYPE = Reflection::getComponentType("renderable");
	if (editor.getUniverse()->hasComponent(entity, MODEL_INSTANCE_TYPE))
	{
		auto* render_interface = editor.getRenderInterface();
		auto path = render_interface->getModelInstancePath(entity);
		if (path.isValid())
		{
			char basename[MAX_PATH_LENGTH];
			copyString(buf, max_size, path.c_str());
			PathUtils::getBasename(basename, MAX_PATH_LENGTH, path.c_str());
			if (name && name[0] != '\0')
				copyString(buf, max_size, name);
			else
				toCString(entity.index, buf, max_size);

			catString(buf, max_size, " - ");
			catString(buf, max_size, basename);
			return;
		}
	}

	if (name && name[0] != '\0')
	{
		copyString(buf, max_size, name);
	}
	else
	{
		toCString(entity.index, buf, max_size);
	}
}


} // namespace Lumix