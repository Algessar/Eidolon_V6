MUST      — absolute requirement
MUST NOT  — absolute prohibition
SHOULD    — recommended; exceptions require justification
MAY       — optional

# Eidolon Engine

Eidolon Engine is a Vulkan backend specific engine. It will never
have any other rendering backend, and building abstraction for that is a 
waste of time.

# Incremental Safety

- The application SHOULD compile after every viable implementation step.
- New types MUST NOT be introduced unless no existing type can fulfill the responsibility.
- Behaviour changes MUST prefer extension to duplication.

# Naming and Ownership



## General naming

Fields:
- public: publicVar;
- private: _myPrivateVar;
- property:  MyProperty {get; set;}
- const: ALL_CAPS
-

Enums
- Flags – ALL_CAPS
- Used as constants – ALL_CAPS
otherwise
- PascalCase

Methods
- Method names MUST be descriptive
- Methods SHOULD NOT contain an unreadable number of params

Events and delegates
- MUST be prefixed with On -> OnExecute, OnUpdate

## Class Renaming
- Classes MUST NOT be renamed by contributors.
- Only the repository owner may perform renames.
- Rename proposals MAY be submitted with a strong justification.
- Approved renames MUST be logged with the reason.

If an existing name violates architectural rules:
- Propose a replacement
- DO NOT apply the rename in generated code

___
# Execution Roles

## Handlers
Handlers are runtime executors.

They:

- Execute runtime or per-frame logic
- MAY allocate transient resources
- MAY own short-lived state

Handlers MUST NOT:

- Perform long-term resource ownership
- Replace factories or managers
  
___
## Managers 

Managers are system-level owners.

They:

- Live for the application lifetime
- Own and track long-lived resources
- Orchestrate creation and destruction

Managers MUST NOT:

- Execute per-frame operational logic
- Act as factories or handlers

## Factories 

Factories are pure creation systems.

They:

- Create GPU or engine resources.
- Produce immutable data objects (e.g. PipelineData, DrawData)

Factories MUST NOT:

- Execute per-frame logic
- Depend on frame state
- Own resources after creation
- Contain runtime behaviour

If runtime execution is required, the system is not a factory.

NOTE: The current Factories may be renamed Builders.
___

## Keys

Keys define object identity and compatibility.

Keys MUST:

- Be immutable
- Contain only data that affects Vulkan compatibility
- Trigger recreation when any value changes

Keys MUST NOT contain:

- Frame index
- Per-frame resource handles
- Allocation strategy
- Engine policy or configuration
- Debug or signature strings
- Any value that changes during normal frame execution

Keys are only used as input to factories.

## Data Objects

Data types are processed carriers, not logic owners.

Data objects SHOULD:
- Be immutable after creation
- Contain no runtime behaviour
- Represent resolved state ready for execution

Data objects SHOULD NOT:
- Handle destruction
- Handle creation

___

# Vulkan Architecture Rules

## Pipelines

- Pipelines are immutable
- Created only through PipelineFactory
- Cached using a key
- Recreated only when the key changes

Pipelines MUST NOT:

- Be created inside frame loops
- Be mutated after creation
- Store temporary or frame state

Architecture changes affecting pipeline ownership or lifecycle MUST NOT be made without explicit discussion and approval.
___

# Frame Execution

Frame code MUST NOT create permanent GPU resources.

Frame code may only:

- Bind
- Record
- Submit

If a change requires persistent resource creation, it MUST be moved to a factory or manager.

___ 
# Forbidden Patterns

The following MUST NOT be generated:

Wrapper types that only mirror Vulkan structs
- Implicit resource creation during Bind() or execution
- Recreating objects every frame instead of caching
- Hidden state machines or implicit lifecycle transitions
- Systems whose responsibility is unclear or overlap existing ones

___

# Structural Gaps

## Lifetime Rule
Resource lifetime ownership MUST be explicit.

Every GPU resource must clearly belong to:

- Manager (long-lived)
- Handler (transient)
- Frame (temporary)

Implicit lifetime is forbidden.

___

## State Visibility Rule

Systems MUST NOT:

_ Mutate hidden global state
_ Perform implicit caching without a visible key
_ Change behaviour based on internal history unless explicitly modelled
___

# Known Architectural Issues
1. BufferFactory / GpuBufferFactory

BufferFactory already produces GpuBuffer.
Creating GpuBufferFactory without a new responsibility is a violation.

GpuBufferFactory is valid only if:

- It performs runtime execution
  and
- It is explicitly defined as a Handler

Otherwise it MUST be removed or merged.

___



