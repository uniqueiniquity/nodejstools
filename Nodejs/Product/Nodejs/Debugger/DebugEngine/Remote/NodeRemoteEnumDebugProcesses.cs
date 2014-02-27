﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Windows.Forms;
using Microsoft.NodejsTools.Debugger.Communication;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Microsoft.NodejsTools.Debugger.Remote {
    internal class NodeRemoteEnumDebugProcesses : NodeRemoteEnumDebug<IDebugProcess2>, IEnumDebugProcesses2 {
        public NodeRemoteEnumDebugProcesses(NodeRemoteDebugPort port, INetworkClientFactory networkClientFactory)
            : base(Connect(port, networkClientFactory)) {
        }

        public NodeRemoteEnumDebugProcesses(NodeRemoteEnumDebugProcesses processes)
            : base(processes.Element) {
        }

        public int Clone(out IEnumDebugProcesses2 ppEnum) {
            ppEnum = new NodeRemoteEnumDebugProcesses(this);
            return VSConstants.S_OK;
        }

        // Connect to the remote debugging server. If any errors occur, display an error dialog, and keep
        // trying for as long as user clicks "Retry".
        private static NodeRemoteDebugProcess Connect(NodeRemoteDebugPort port, INetworkClientFactory networkClientFactory) {
            NodeRemoteDebugProcess process = null;
            while (true) {
                try {
                    using (var client = networkClientFactory.CreateNetworkClient(port.Uri))
                    using (var stream = client.GetStream()) {
                        // https://nodejstools.codeplex.com/workitem/578
                        // Read "welcome" headers from node debug socket before disconnecting to workaround issue
                        // where connect and immediate disconnect leaves node.js (V8) in a bad state which blocks attach.
                        var buffer = new byte[1024];
                        if (stream.ReadAsync(buffer, 0, buffer.Length).Wait(5000)) {
                            process = new NodeRemoteDebugProcess(port, "node.exe", "", "");
                            break;
                        }
                    }
                } catch (IOException) {
                } catch (SocketException) {
                } catch (WebSocketException) {
                }

                string errText =
                    string.Format(
                        "Could not attach to Node.js process at {0}. " +
                        "Make sure the process is running behind the remote debug proxy (RemoteDebug.js), " +
                        "and the debugger port (default {1}) is open on the target host.",
                        port.Uri, NodejsConstants.DefaultDebuggerPort);
                DialogResult dlgRes = MessageBox.Show(errText, null, MessageBoxButtons.RetryCancel, MessageBoxIcon.Error);
                if (dlgRes != DialogResult.Retry) {
                    break;
                }
            }

            return process;
        }
    }
}
