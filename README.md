# CppToCsBridge
Code generation tool that automatically generates bindings needed to allow for interop between C++ classes and C# code.

## Current Plans for tool
* Figure out how to make classes with parent classes to work.
* Currently operator functions cause invalid names and need to be fixed. One solution is to instead use a UUID or something.
*    > Maybe just state you can't apply the function on operator types?
* Template functions also currently don't really work. Decide if we should just not allow them for generation?
*    > Undecided but likely not.

## How to use the generation tool:

You need to define a macro to define what items the tool is allowed to touch. Here is an example.

These are the macros you need first.
```cpp
    #pragma once

    // NOLINTBEGIN
    #ifndef __cppast
    #define __cppast(...)
    #endif
    // NOLINTEND

    #ifndef IMP_BRIDGE_CLASS
    #define IMP_BRIDGE_CLASS __cppast(bridge_class)
    #endif

    #ifndef IMP_BRIDGE_FUNC
    #define IMP_BRIDGE_FUNC __cppast(bridge_func)
    #endif

```

Then all you have to do is this and the tool will handle the rest.

```cpp
#pragma once

#include "bridge.hpp"

namespace TestNamespace {
    class IMP_BRIDGE_CLASS SimpleClass {
    public:
        IMP_BRIDGE_FUNC
        SimpleClass() {}

        IMP_BRIDGE_FUNC
        ~SimpleClass() {}

        IMP_BRIDGE_FUNC
        void SayHello() { }

        IMP_BRIDGE_FUNC
        int AddNumbers(int a, int b) { return a + b; }
    };
}
```

Then  the following C++ code will be generated

```cpp
// THIS IS GENERATED CODE DO NOT EDIT DIRECTLY
// FILE USED FOR GENERATION: testcode.hpp
// GENERATION DATE: 2024-12-31 01:23
// clang-format off
// NOLINTBEGIN
#pragma once

#include "impact/engine.hpp"
#include <utility>
extern "C" {
namespace TestNamespace {
typedef void* SimpleClassHandle;
inline void* SimpleClass_Create() { return reinterpret_cast<SimpleClassHandle>(new SimpleClass()); }
inline void SimpleClass_Destroy(SimpleClassHandle handle) { delete reinterpret_cast<SimpleClass*>(handle); }
inline void SimpleClass_Call(SimpleClassHandle handle, uint32_t methodID, void* param) {
    auto* instance = reinterpret_cast<SimpleClass*>(handle);
    switch (methodID) {
        case 0:
            instance->SayHello();
            break;
        case 1:
            using ArgsType_AddNumbers_1 = std::tuple<int, int>;
            auto* args_AddNumbers_1 = reinterpret_cast<ArgsType_AddNumbers_1*>(param);
            std::apply([&](auto&&... args) { instance->AddNumbers(std::forward<decltype(args)>(args)...); }, *args_AddNumbers_1);            break;
        default:
            break;
    }
}
} // namespace TestNamespace
} // extern c
// NOLINTEND
// clang-format on

```

Currently the tool does not yet generate the required P/Invoke for the C# code but this feature is planned at some point.