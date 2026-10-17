#!/bin/bash
# Claude Code 云端会话的启动钩子：安装 .NET 10 SDK 并还原 NuGet 包，
# 让会话里能直接跑 Tools/ 下的 dotnet test 和模拟器（策划/09 §5 M0.1）。
#
# 只在云端会话里运行（CLAUDE_CODE_REMOTE=true），本机会话直接退出。
# 可以重复执行：SDK 已装好就跳过安装，restore 本身是幂等的。
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/../.." && pwd)}"

has_sdk10() {
  command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'
}

as_root() {
  if [ "$(id -u)" -eq 0 ]; then
    "$@"
  elif command -v sudo >/dev/null 2>&1; then
    sudo -n "$@"
  else
    echo "session-start: 需要 root 或免密 sudo 才能安装 .NET SDK" >&2
    return 1
  fi
}

# 云端容器的网络策略拦截了 builds.dotnet.microsoft.com（dotnet-install.sh 用的下载站），
# 所以从 Ubuntu 官方源装：24.04 的 noble-updates 里有 dotnet-sdk-10.0。
if ! has_sdk10; then
  export DEBIAN_FRONTEND=noninteractive
  if ! apt-cache show dotnet-sdk-10.0 >/dev/null 2>&1; then
    as_root apt-get update -q
  fi
  as_root apt-get install -y -q --no-install-recommends dotnet-sdk-10.0
fi

if ! has_sdk10; then
  echo "session-start: .NET 10 SDK 安装失败，dotnet test 无法运行" >&2
  exit 1
fi

if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  {
    echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
    echo 'export DOTNET_NOLOGO=1'
    echo 'export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1'
  } >> "$CLAUDE_ENV_FILE"
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# 预先还原 NuGet 包（NUnit 等），钩子跑完后容器状态会被缓存，之后的会话不用再下载。
dotnet restore "$PROJECT_DIR/Tools/Deeptide.slnx"
