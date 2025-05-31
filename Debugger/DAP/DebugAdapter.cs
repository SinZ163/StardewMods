using ContentPatcher.Framework;
using ContentPatcher.Framework.Patches;
using ContentPatcher.Framework.Tokens;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using StardewModdingAPI.Utilities;
using System.Collections;
using System.Reflection;
using StackFrame = Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.StackFrame;
using Thread = Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.Thread;

namespace SinZ.Debugger.DAP;

public class DebugAdapter : DebugAdapterBase
{
    private bool isStopped = false;

    private int VariableReference = 1;

    private Scope GlobalScope;
    private Dictionary<int, List<Scope>> LocalScopes;
    private List<Thread> Threads;
    private Dictionary<int, List<StackFrame>> StackFrames;
    private Dictionary<int, List<Variable>> Variables;
    private Dictionary<string, int> ContentPackToStackFrame;

    private Dictionary<string, Dictionary<IPatch, PatchConfigExtended>> Breakpoints = new();

    private ContentPatcher.ModEntry cpMod;
    public DebugAdapter(Stream streamIn, Stream streamOut, ContentPatcher.ModEntry cpMod)
    {
        InitializeProtocolClient(streamIn, streamOut);
        this.cpMod = cpMod;
    }

    private void PopulateToken(IToken token, int parentVariableId)
    {
        if (token.RequiresInput)
        {
            Variables[parentVariableId].Add(new Variable(token.Name, "<needs input>", 0));
        }
        else
        {
            int variableId = 0;
            if (!token.IsReady)
            {
                var unreadyVariableId = ++VariableReference;
                Variables.Add(unreadyVariableId, new());
                Variables[parentVariableId].Add(new Variable(token.Name, "<not ready>", unreadyVariableId));
                var i = 0;
                foreach (var unready in token.GetDiagnosticState().UnreadyTokens)
                {
                    Variables[unreadyVariableId].Add(new Variable($"unready:#{i++}", unready, 0));
                }
                i = 0;
                foreach (var invalid in token.GetDiagnosticState().InvalidTokens)
                {
                    Variables[unreadyVariableId].Add(new Variable($"invalid:#{i++}", invalid, 0));
                }
                i = 0;
                foreach (var unavailable in token.GetDiagnosticState().UnavailableModTokens)
                {
                    Variables[unreadyVariableId].Add(new Variable($"unavailable:#{i++}", unavailable, 0));
                }
                return;
            }
            var tokenValues = token.GetValues(InputArguments.Empty);
            if (tokenValues.Count == 0)
            {
                Variables[parentVariableId].Add(new Variable(token.Name, "<empty>", variableId));
                return;
            }
            if (tokenValues.Count > 1)
            {
                variableId = ++VariableReference;
                Variables.Add(variableId, new());
                var i = 0;
                foreach (var value in tokenValues)
                {
                    Variables[variableId].Add(new Variable($"#{i++}", value, 0));
                }
            }
            Variables[parentVariableId].Add(new Variable(token.Name, string.Join(", ", tokenValues), variableId));
        }
    }

    private void PopulateInitialState()
    {
        var packs = cpMod.Helper.ContentPacks.GetOwned();
        Threads = new();
        StackFrames = new();
        LocalScopes = new();
        Variables = new();
        var threadId = 0;
        var stackFrameId = 0;

        ContentPackToStackFrame = new Dictionary<string, int>();

        var tokenStateThread = new Thread()
        {
            Id = ++threadId,
            Name = "Token State",
        };
        Threads.Add(tokenStateThread);
        foreach (var pack in packs)
        {
            ContentPackToStackFrame.Add(pack.Manifest.UniqueID, ++stackFrameId);
            StackFrames.Add(tokenStateThread.Id, new()
            {
                new(stackFrameId, pack.Manifest.Name + " Token State", 0, 0)
                {
                    Source = new()
                    {
                        Path = Path.Combine(pack.DirectoryPath, "content.json"),
                        Name = pack.Manifest.Name,
                    },
                    Line = 10,
                    EndLine = 100,
                    Column = 10,
                    EndColumn = 100,
                    PresentationHint = StackFrame.PresentationHintValue.Subtle
                }
            });
            Thread? packThread = null;
            foreach (var filename in Breakpoints.Keys)
            {
                if (filename.StartsWith(pack.DirectoryPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    foreach ((var patch, var patchEntry) in Breakpoints[filename])
                    {
                        packThread ??= new Thread(++threadId, pack.Manifest.Name);
                        StackFrames[packThread.Id].Add(new(++stackFrameId, patch.Path.ToString(), 0, 0)
                        {
                            Source = new()
                            {
                                Path = filename,
                                Name = Path.GetFileName(filename),
                            },
                            Line = patchEntry.Debugger_LineNumberRange.StartLineNumber,
                            EndLine = patchEntry.Debugger_LineNumberRange.StartLineColumn,
                            Column = patchEntry.Debugger_LineNumberRange.EndLineNumber,
                            EndColumn = patchEntry.Debugger_LineNumberRange.EndLineColumn,
                            PresentationHint = StackFrame.PresentationHintValue.Normal
                        });
                        var usedTokens = new Scope()
                        {
                            Name = "Consumed Tokens",
                            VariablesReference = ++VariableReference,
                        };
                        Variables[usedTokens.VariablesReference] = new();
                        var patchFieldTokens = new Scope()
                        {
                            Name = "Patch Field Tokens",
                            VariablesReference = ++VariableReference,
                        };
                        Variables[patchFieldTokens.VariablesReference] = new();
                        var customLocalTokens = new Scope()
                        {
                            Name = "Local Tokens",
                            VariablesReference = ++VariableReference,
                        };
                        Variables[customLocalTokens.VariablesReference] = new();
                        var inheritedLocalTokens = new Scope()
                        {
                            Name = "Inherited Local Tokens",
                            VariablesReference = ++VariableReference,
                        };
                        Variables[inheritedLocalTokens.VariablesReference] = new();
                        LocalScopes[stackFrameId] = new() { usedTokens, patchFieldTokens };

                        var patchFieldTokensContext = typeof(Patch).GetField("PatchFieldTokensContext", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(patch) as LocalContext;
                        foreach (var token in patchFieldTokensContext.GetTokens(false))
                        {
                            PopulateToken(token, patchFieldTokens.VariablesReference);
                        }
                        var customLocalFieldTokensContext = typeof(Patch).GetField("CustomLocalTokensContext", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(patch) as LocalContext;
                        if (customLocalFieldTokensContext != null)
                        {
                            foreach (var token in patchFieldTokensContext.GetTokens(false))
                            {
                                PopulateToken(token, customLocalTokens.VariablesReference);
                            }
                            LocalScopes[stackFrameId].Add(customLocalTokens);
                        }
                        var inheritedLocalTokensContext = typeof(Patch).GetField("InheritedLocalTokensContext", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(patch) as LocalContext;
                        if (inheritedLocalTokensContext != null)
                        {
                            foreach (var token in inheritedLocalTokensContext.GetTokens(false))
                            {
                                PopulateToken(token, inheritedLocalTokens.VariablesReference);
                            }
                            LocalScopes[stackFrameId].Add(inheritedLocalTokens);
                        }

                        var useContext = customLocalFieldTokensContext ?? patchFieldTokensContext;
                        foreach (var tokenName in patch.GetTokensUsed())
                        {
                            var token = useContext.GetToken(tokenName, false);
                            if (token != null)
                            {
                                PopulateToken(token, usedTokens.VariablesReference);
                            }
                            else
                            {
                                Variables[usedTokens.VariablesReference].Add(new Variable(tokenName, "<not loaded>", 0));
                            }
                        }

                    }
                }
            }
        }
        PopulateVariables();
    }
    public void PopulateVariables()
    {
        var screenManagerWrapper = typeof(ContentPatcher.ModEntry).GetField("ScreenManager", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cpMod) as PerScreen<ScreenManager>;
        var globalContext = typeof(TokenManager).GetField("GlobalContext", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(screenManagerWrapper.Value.TokenManager) as GenericTokenContext;
        var localTokens = typeof(TokenManager).GetField("LocalTokens", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(screenManagerWrapper.Value.TokenManager) as IDictionary;

        GlobalScope = new Scope("Global", ++VariableReference, false)
        {
            Column = 10,
            EndColumn = 100,
            Line = 10,
            EndLine = 100,
        };
        Variables.Add(GlobalScope.VariablesReference, new());
        foreach (var token in globalContext.Tokens.Values)
        {
            PopulateToken(token, GlobalScope.VariablesReference);
        }

        foreach (var local in localTokens.Values)
        {
            var modContext = local.GetType().GetProperty("Context", BindingFlags.Public | BindingFlags.Instance).GetValue(local) as ModTokenContext;

            var scope = typeof(ModTokenContext).GetField("Scope", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(modContext) as string;

            var localScope = new Scope("Local", ++VariableReference, false)
            {
                Column = 10,
                EndColumn = 100,
                Line = 10,
                EndLine = 100,
            };
            var localContext = typeof(ModTokenContext).GetField("LocalContext", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(modContext) as GenericTokenContext;
            Variables.Add(localScope.VariablesReference, new());
            foreach (var token in localContext.GetTokens(false))
            {
                PopulateToken(token, localScope.VariablesReference);
            }

            var dynamicScope = new Scope("Dynamic Tokens", ++VariableReference, false)
            {
                Column = 10,
                EndColumn = 100,
                Line = 10,
                EndLine = 100,
            };
            var dynamicContext = typeof(ModTokenContext).GetField("DynamicContext", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(modContext) as GenericTokenContext;
            Variables.Add(dynamicScope.VariablesReference, new());
            foreach (var token in dynamicContext.GetTokens(false))
            {
                PopulateToken(token, dynamicScope.VariablesReference);
            }

            LocalScopes.Add(ContentPackToStackFrame[scope], new() { GlobalScope, localScope, dynamicScope });
        }
    }

    protected override InitializeResponse HandleInitializeRequest(InitializeArguments args)
    {
        Protocol.SendEvent(new InitializedEvent());
        return new InitializeResponse()
        {
            //SupportsEvaluateForHovers = true,
            SupportsCompletionsRequest = true,
            SupportsConfigurationDoneRequest = true,
        };
    }
    protected override ConfigurationDoneResponse HandleConfigurationDoneRequest(ConfigurationDoneArguments arguments)
    {
        return new ConfigurationDoneResponse();
    }

    protected override SetBreakpointsResponse HandleSetBreakpointsRequest(SetBreakpointsArguments arguments)
    {
        var breakpoints = new List<Breakpoint>();
        string? FailureReason = null;
        Breakpoints.Remove(arguments.Source.Path);
        // TODO: Handle loading/pending states
        // TODO: Handle breakpoints changing and needing to repopulate threads
        if (!DebugAdapterManager.SourceMap.TryGetValue(arguments.Source.Path, out var map))
        {
            FailureReason = "Untracked file";
        }
        Breakpoints.Add(arguments.Source.Path, new());
        foreach (var request in arguments.Breakpoints)
        {
            if (FailureReason != null)
            {
                breakpoints.Add(new Breakpoint(false)
                {
                    Message = FailureReason,
                });
                continue;
            }
            var foundBreakpoint = false;
            foreach ((var patch, var patchentry) in map)
            {
                if (request.Line >= patchentry.Debugger_LineNumberRange.StartLineNumber && request.Line <= patchentry.Debugger_LineNumberRange.EndLineNumber)
                {
                    foundBreakpoint = true;
                    breakpoints.Add(new Breakpoint()
                    {
                        Verified = true,
                        Line = patchentry.Debugger_LineNumberRange.StartLineNumber,
                        Column = patchentry.Debugger_LineNumberRange.StartLineColumn,
                        EndLine = patchentry.Debugger_LineNumberRange.EndLineNumber,
                        EndColumn = patchentry.Debugger_LineNumberRange.EndLineColumn
                    });
                    Breakpoints[arguments.Source.Path].Add(patch, patchentry);
                }
            }
            if (!foundBreakpoint)
            {
                breakpoints.Add(new Breakpoint(false)
                {
                    Message = "Unknown patch at this location",
                });
            }
        }
        return new SetBreakpointsResponse(breakpoints);
    }

    protected override CompletionsResponse HandleCompletionsRequest(CompletionsArguments arguments)
    {
        //ModEntry.Log(JsonSerializer.Serialize(arguments));
        return new CompletionsResponse();
    }
    protected override LaunchResponse HandleLaunchRequest(LaunchArguments arguments)
    {
        return new LaunchResponse()
        {
        };
    }
    protected override SetExceptionBreakpointsResponse HandleSetExceptionBreakpointsRequest(SetExceptionBreakpointsArguments arguments)
    {
        return new SetExceptionBreakpointsResponse();
    }
    protected override ThreadsResponse HandleThreadsRequest(ThreadsArguments arguments)
    {
        if (!isStopped)
        {
            PopulateInitialState();
            Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Entry) { Description = "Debug View", AllThreadsStopped = true, PreserveFocusHint = true });
            isStopped = true;
        }
        return new ThreadsResponse(Threads);
    }
    protected override StackTraceResponse HandleStackTraceRequest(StackTraceArguments arguments)
    {
        if (StackFrames.TryGetValue(arguments.ThreadId, out var stackFrame))
        {
            return new StackTraceResponse(stackFrame);
        }
        return new StackTraceResponse();
    }
    protected override ScopesResponse HandleScopesRequest(ScopesArguments arguments)
    {
        if (LocalScopes.TryGetValue(arguments.FrameId, out var scopes))
        {
            return new ScopesResponse(scopes);
        }
        return new ScopesResponse();
    }
    protected override VariablesResponse HandleVariablesRequest(VariablesArguments arguments)
    {
        if (Variables.TryGetValue(arguments.VariablesReference, out var variable))
        {
            return new VariablesResponse(variable);
        }
        return new VariablesResponse();
    }
    protected override EvaluateResponse HandleEvaluateRequest(EvaluateArguments arguments)
    {
        // TODO: Do something?
        return new EvaluateResponse()
        {
        };
    }

    public void Run()
    {
        Protocol.Run();
    }

    internal void UpdateContext()
    {
        //PopulateVariables();
        Protocol.SendEvent(new InvalidatedEvent() { Areas = new() { InvalidatedAreas.Variables } });
    }
}
