#!/usr/bin/env bash
# Legacy: provisions Foundry toolboxes and hosted agent versions via azd.
# The active deployment path uses agent-provisioning/ instead.
set -euo pipefail

AGENT_NAMES=(
  document-processing-agent
  underwriting-agent
  responsible-ai-agent
  loan-setup-agent
)

TOOLBOX_NAMES=(
  document-retrieval-toolbox
  underwriting-rules-toolbox
  policy-knowledge-toolbox
  loan-setup-toolbox
)

TOOLBOX_DESCRIPTIONS=(
  "Document retrieval MCP tools for document-processing-agent."
  "Underwriting rules MCP tools for underwriting-agent."
  "Policy knowledge MCP tools for responsible-ai-agent."
  "Loan setup MCP tools for loan-setup-agent."
)

TOOLBOX_LABELS=(
  document_retrieval
  underwriting_rules
  policy_knowledge
  loan_setup
)

TOOLBOX_PATHS=(
  /document-retrieval/mcp
  /underwriting-rules/mcp
  /policy-knowledge/mcp
  /loan-setup/mcp
)

AGENT_DESCRIPTIONS=(
  "Processes loan documents and returns structured JSON."
  "Evaluates loan risk and returns an underwriting recommendation."
  "Reviews fairness, governance, and responsible AI concerns."
  "Prepares the final loan setup package."
)

AGENT_DISPLAY_NAMES=(
  "Document Processing Agent"
  "Underwriting Agent"
  "Responsible AI Agent"
  "Loan Setup Agent"
)

require_env() {
  local name="$1"
  if [ -z "${!name:-}" ]; then
    echo "Missing required environment variable: ${name}" >&2
    exit 1
  fi
}

install_azd() {
  if command -v azd >/dev/null 2>&1; then
    echo "azd already installed: $(azd version)"
    return
  fi

  echo "Installing Azure Developer CLI..."
  apk add --no-cache curl
  curl -fsSL https://aka.ms/install-azd.sh | bash
  export PATH="/usr/local/bin:${PATH}"
  echo "Installed azd: $(azd version)"
}

verify_azd_auth_login() {
  export PATH="/usr/local/bin:${PATH}"

  echo "Verifying azd auth login with managed identity..."
  if ! azd auth login --managed-identity --client-id "${DEPLOYMENT_SCRIPT_CLIENT_ID}"; then
    echo "azd auth login failed." >&2
    exit 1
  fi

  if ! azd auth login --check-status >/dev/null 2>&1; then
    echo "azd auth login check-status failed." >&2
    azd auth login --check-status >&2 || true
    exit 1
  fi

  echo "azd auth login verified successfully."
}

write_azure_yaml_for_agent() {
  local agent_name="$1"

  cat > "${AZD_WORK_DIR}/azure.yaml" <<EOF
name: cohereloan-hosted-agents
metadata:
  template: cohereloan-loan-mortgage-hosted-agents
requiredVersions:
  extensions:
    azure.ai.agents: latest
services:
  ${agent_name}:
    project: agents/${agent_name}
    host: azure.ai.agent
EOF
}

write_agent_manifest() {
  local agent_name="$1"
  local display_name="$2"
  local description="$3"
  local target_dir="${AZD_WORK_DIR}/agents/${agent_name}"

  mkdir -p "${target_dir}"
  cat > "${target_dir}/agent.manifest.yaml" <<EOF
name: ${agent_name}
displayName: ${display_name}
description: ${description}
template:
  name: ${agent_name}
  kind: hosted
  protocols:
    - protocol: responses
      version: 1.0.0
  resources:
    cpu: "0.5"
    memory: 1Gi
parameters:
  properties: []
resources:
  - kind: model
    id: ${MODEL_DEPLOYMENT_NAME}
    name: ${MODEL_DEPLOYMENT_NAME}
EOF
}

configure_azd() {
  export PATH="/usr/local/bin:${PATH}"
  azd config set alpha.extensions on
  azd extension install azure.ai.agents
  azd extension install azure.ai.toolboxes

  local work_dir="${AZD_WORK_DIR:-/tmp/cohereloan-hosted-agents}"
  local env_name="${AZD_ENV_NAME:-cohereloan-deploy}"

  mkdir -p "${work_dir}/agents" "${work_dir}/toolboxes" "${work_dir}/.azure/${env_name}"
  export AZD_WORK_DIR="${work_dir}"
  cd "${work_dir}"

  write_azure_yaml_for_agent "${AGENT_NAMES[0]}"

  local index=0
  for agent_name in "${AGENT_NAMES[@]}"; do
    write_agent_manifest \
      "${agent_name}" \
      "${AGENT_DISPLAY_NAMES[$index]}" \
      "${AGENT_DESCRIPTIONS[$index]}"
    index=$((index + 1))
  done

  write_all_agent_yamls

  cat > "${work_dir}/.azure/config.json" <<EOF
{"version":1,"defaultEnvironment":"${env_name}"}
EOF

  cat > "${work_dir}/.azure/${env_name}/.env" <<EOF
AZURE_ENV_NAME="${env_name}"
AZURE_SUBSCRIPTION_ID="${AZURE_SUBSCRIPTION_ID}"
AZURE_RESOURCE_GROUP="${AZURE_RESOURCE_GROUP}"
AZURE_LOCATION="${AZURE_LOCATION}"
AZURE_AI_PROJECT_ENDPOINT="${FOUNDRY_PROJECT_ENDPOINT}"
AZURE_FOUNDRY_PROJECT_ENDPOINT="${FOUNDRY_PROJECT_ENDPOINT}"
MODEL_DEPLOYMENT_NAME="${MODEL_DEPLOYMENT_NAME}"
AZURE_AI_MODEL_DEPLOYMENT_NAME="${MODEL_DEPLOYMENT_NAME}"
HOSTED_AGENT_IMAGE="${HOSTED_AGENT_IMAGE}"
HOSTED_AGENT_DEPLOY_REVISION="${HOSTED_AGENT_DEPLOY_REVISION}"
EOF

  azd env select "${env_name}" --no-prompt || true

  azd env set AZURE_SUBSCRIPTION_ID "${AZURE_SUBSCRIPTION_ID}" --no-prompt
  azd env set AZURE_RESOURCE_GROUP "${AZURE_RESOURCE_GROUP}" --no-prompt
  azd env set AZURE_LOCATION "${AZURE_LOCATION}" --no-prompt
  azd env set AZURE_AI_PROJECT_ENDPOINT "${FOUNDRY_PROJECT_ENDPOINT}" --no-prompt
  azd env set AZURE_FOUNDRY_PROJECT_ENDPOINT "${FOUNDRY_PROJECT_ENDPOINT}" --no-prompt
  azd env set MODEL_DEPLOYMENT_NAME "${MODEL_DEPLOYMENT_NAME}" --no-prompt
  azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME "${MODEL_DEPLOYMENT_NAME}" --no-prompt
  azd env set HOSTED_AGENT_IMAGE "${HOSTED_AGENT_IMAGE}" --no-prompt
  azd env set HOSTED_AGENT_DEPLOY_REVISION "${HOSTED_AGENT_DEPLOY_REVISION}" --no-prompt
}

write_agent_yaml() {
  local agent_name="$1"
  local description="$2"
  local target_dir="${AZD_WORK_DIR}/agents/${agent_name}"

  mkdir -p "${target_dir}"
  cat > "${target_dir}/agent.yaml" <<EOF
kind: hosted
name: ${agent_name}
description: ${description}
protocols:
  - protocol: responses
    version: 1.0.0
resources:
  cpu: "0.5"
  memory: 1Gi
image: ${HOSTED_AGENT_IMAGE}
environment_variables:
  - name: AZURE_AI_PROJECT_ENDPOINT
    value: ${FOUNDRY_PROJECT_ENDPOINT}
  - name: MODEL_DEPLOYMENT_NAME
    value: ${MODEL_DEPLOYMENT_NAME}
  - name: AZURE_AI_MODEL_DEPLOYMENT_NAME
    value: ${MODEL_DEPLOYMENT_NAME}
  - name: HOSTED_AGENT_DEPLOY_REVISION
    value: ${HOSTED_AGENT_DEPLOY_REVISION}
EOF
}

write_all_agent_yamls() {
  local index=0
  for agent_name in "${AGENT_NAMES[@]}"; do
    write_agent_yaml "${agent_name}" "${AGENT_DESCRIPTIONS[$index]}"
    index=$((index + 1))
  done
}

write_toolbox_yaml() {
  local toolbox_name="$1"
  local description="$2"
  local server_label="$3"
  local mcp_path="$4"
  local target_file="${AZD_WORK_DIR}/toolboxes/${toolbox_name}.yaml"

  cat > "${target_file}" <<EOF
description: ${description}
tools:
  - type: mcp
    server_label: ${server_label}
    server_url: ${MCP_BASE_URL}${mcp_path}
    require_approval: never
EOF
}

agent_exists() {
  local agent_name="$1"

  azd ai agent show "${agent_name}" \
    --project-endpoint "${FOUNDRY_PROJECT_ENDPOINT}" \
    --no-prompt >/dev/null 2>&1 \
    || azd ai agent show "${agent_name}" \
      --project-id "${FOUNDRY_PROJECT_ARM_ID}" \
      --no-prompt >/dev/null 2>&1
}

register_agent_if_needed() {
  local agent_name="$1"
  local manifest_path="agents/${agent_name}/agent.manifest.yaml"

  if agent_exists "${agent_name}"; then
    echo "Agent ${agent_name} already exists; skipping init."
    return
  fi

  echo "Registering agent ${agent_name}..."
  azd ai agent init \
    --no-prompt \
    --project-id "${FOUNDRY_PROJECT_ARM_ID}" \
    -m "${manifest_path}"
}

toolbox_exists() {
  local toolbox_name="$1"

  azd ai toolbox show "${toolbox_name}" \
    --project-endpoint "${FOUNDRY_PROJECT_ENDPOINT}" \
    --no-prompt >/dev/null 2>&1
}

create_toolbox() {
  local toolbox_name="$1"
  local description="$2"
  local server_label="$3"
  local mcp_path="$4"
  local toolbox_file="toolboxes/${toolbox_name}.yaml"

  write_toolbox_yaml "${toolbox_name}" "${description}" "${server_label}" "${mcp_path}"

  if toolbox_exists "${toolbox_name}"; then
    echo "Toolbox ${toolbox_name} already exists; skipping version update (azd-only flow)."
    return
  fi

  echo "Creating toolbox ${toolbox_name}..."
  azd ai toolbox create "${toolbox_name}" \
    --from-file "${toolbox_file}" \
    --project-endpoint "${FOUNDRY_PROJECT_ENDPOINT}" \
    --no-prompt
}

deploy_agent() {
  local agent_name="$1"
  local description="$2"

  write_agent_yaml "${agent_name}" "${description}"
  register_agent_if_needed "${agent_name}"
  write_azure_yaml_for_agent "${agent_name}"

  echo "Deploying hosted agent ${agent_name} with pre-built image ${HOSTED_AGENT_IMAGE}..."
  azd deploy "${agent_name}" --from-package "${HOSTED_AGENT_IMAGE}" --no-prompt
}

main() {
  require_env AZURE_SUBSCRIPTION_ID
  require_env AZURE_RESOURCE_GROUP
  require_env AZURE_LOCATION
  require_env DEPLOYMENT_SCRIPT_CLIENT_ID
  require_env FOUNDRY_PROJECT_ENDPOINT
  require_env FOUNDRY_PROJECT_ARM_ID
  require_env MODEL_DEPLOYMENT_NAME
  require_env HOSTED_AGENT_IMAGE
  require_env HOSTED_AGENT_DEPLOY_REVISION
  require_env MCP_BASE_URL

  echo "Waiting briefly for role assignments and Foundry deployments to settle..."
  sleep 60

  install_azd
  verify_azd_auth_login
  configure_azd

  local index=0
  for toolbox_name in "${TOOLBOX_NAMES[@]}"; do
    create_toolbox \
      "${toolbox_name}" \
      "${TOOLBOX_DESCRIPTIONS[$index]}" \
      "${TOOLBOX_LABELS[$index]}" \
      "${TOOLBOX_PATHS[$index]}"
    index=$((index + 1))
  done

  index=0
  for agent_name in "${AGENT_NAMES[@]}"; do
    deploy_agent "${agent_name}" "${AGENT_DESCRIPTIONS[$index]}"
    index=$((index + 1))
  done

  echo "Hosted agent versions were submitted successfully via azd."
}

main "$@"
