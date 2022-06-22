---
uid: Guides.TextCommands.Intro
title: 文本命令框架入门
---

# 文本命令框架

[KaiHeiLa.Commands](xref:KaiHeiLa.Commands) 命名空间提供了一组基于特性标签的文本命令服务。

## 入门

要使用文本命令框架，需要先创建 [CommandService] 及命令处理程序。

下面的代码演示了如何创建命令服务和命令处理程序的最小程序，可在此基础上扩展功能模块。

> [!NOTE]
> `CommandService` 可接收一个 [CommandServiceConfig] 类型的可选参数用于命令服务的配置，
> 如果不提供此参数，则使用默认配置。建议在使用配置时，通过 IDE 的自动提示与代码注释
> 浏览并了解各项属性与默认参数值，并在需要时修改。

[!code-csharp[Command Handler](samples/intro/command_handler.cs)]

[CommandService]: xref:KaiHeiLa.Commands.CommandService
[CommandServiceConfig]: xref:KaiHeiLa.Commands.CommandServiceConfig

## 使用特性

文本命令框架支持通过特性标签来预先定义命令，也可以在运行时通过命令构建类来定义命令。

绝大多数情况下，Bot 的命令服务的确定的，因此推荐使用特性标签来定义命令。

### 模块

命令的功能由模块定义，在不同的类中可以分别定义不同的命令功能，在启动时可以一并加载。

要使模块能够被自动发现，模块需要满足：

- 公开
- 继承自 [ModuleBase]

则模块类的声明形如：

[!code-csharp[Empty Module](samples/intro/empty-module.cs)]

> [!NOTE]
> [ModuleBase] 是一个抽象类，可根据需要进行派生或重写。
> 模块可以继承自 ModuleBase 的任何派生类。

[ModuleBase]: xref:KaiHeiLa.Commands.ModuleBase`1

### 添加/创建命令

> [!WARNING]
> 尽量避免模型内代码运行耗时过长，这可能会导致网关线程的阻塞，进而中断 Bot 与开黑啦服务端的连接。

创建命令的处理程序，方法的返回类型必须是 `Task` 或 `Task<RuntimeResult>`，按需标记 `async` 关键字。

对该方法标记 [CommandAttribute] 特性标签，并指定命令的名称。

如该方法是 [模块组](#模块组) 中的命令，名称可以留空。

### 命令参数

命令处理函数的参数即为命令的参数，例如：

- 整型作为参数：`int num`
- 用户作为参数：`IUser user`

命令参数几乎可以是任何类型的，默认支持读取的类型列表参见 @Guides.TextCommands.TypeReaders 。

#### 可选参数

默认地，命令参数为必选参数，要设置为可选参数，需为其指定默认值，例如：`int num = 0`。

#### 参数中的空格

如要接收一个以空格分隔的列表，可指定可变参数关键字 `params`，例如：`params int[]`。

如果实参包含空格，在调用命令时，该实参应以双引号进行包装，
例如：对于参数 `string food`，可通过 `!favoritefood "Key Lime Pie"` 的形式进行调用。

如果某个参数可以包含空格地持续读取到命令末尾，可以为该参数标记 [RemainderAttribute]，
用户在调用时便不必将实参包装在双引号中。

[RemainderAttribute]: xref:KaiHeiLa.Commands.RemainderAttribute

### 命令重载

命令处理函数支持重载，命令解析过程将自动地选择类型匹配的方法。

如果两个命令处理函数间在调用时会存在不明确的引用，
可为应优先尝试进行类型匹配的重载标记 @KaiHeiLa.Commands.PriorityAttribute 。

### 命令上下文

每个命令都可以通过 [ModuleBase] 上的 [Context] 属性访问执行上下文。
`ICommandContext` 支持访问消息、频道、服务器、用户、以及调用命令的底层开黑啦客户端。

使用 [ModuleBase] 的派生类可以指定不同类型的上下文。
例如，[SocketCommandContext] 中的上下文中的属性为是 Socket 实体，不再需要进行强制类型转换。

回复消息也可通过调用以 `Reply` 为前缀的方法完成，无需调用上下文中频道内发送消息的方法来回复命令。

> [!WARNING]
> 上下文的类型不应该混合使用，使用 `CommandContext` 上下文的模块与使用 `SocketCommandContext` 
> 的模块不可同时使用。

> [!TIP]
> 模块的完整代码示例：
> [!code-csharp[Example Module](samples/intro/module.cs)]

#### 自动加载模块

命令服务可以自动发现并加载程序集内继承自 [ModuleBase] 的类，调用 [CommandService.AddModulesAsync]
可以进行自动发现与模块加载。如果要指定某个模块跳过自动加载，可对其标记特性 [DontAutoLoadAttribute]。

[DontAutoLoadAttribute]: xref:KaiHeiLa.Commands.DontAutoLoadAttribute
[CommandService.AddModulesAsync]: xref:KaiHeiLa.Commands.CommandService.AddModulesAsync*

#### 手动加载模块

要手动加载模块，可调用 [CommandService.AddModuleAsync]，模块的类通过泛型传递。
如有需要，也可以向该方法传入 IServiceProvider.

[CommandService.AddModuleAsync]: xref:KaiHeiLa.Commands.CommandService.AddModuleAsync*

### 模块构造函数

模块通过 [依赖注入](xref:Guides.TextCommands.DI) 进行构造，
构造函数内的任何参数都应先注入服务容器中。

> [!TIP]
> 模块构造函数也可以将 `IServiceProvider` 作为参数，并从中手动抽取所需服务，但并不推荐。

### 模块属性

具有公共 `set` 访问器属性的模块在模块构造后会被服务容器中，详细信息可参考 @Guides.TextCommands.DI 。

### 模块组

模块组为一组具有相同名称前缀的命令，要声明模块组，可标记特性 @KaiHeiLa.Commands.GroupAttribute 。

在模块组内，如果命令的 CommandAttribute 特性标签中不传入名称参数，则为创建以该组名称作为命令名称的命令。

### 子模块

子模块是模块内的模块，可用于创建嵌套组。

[!code-csharp[Groups and Submodules](samples/intro/groups.cs)]