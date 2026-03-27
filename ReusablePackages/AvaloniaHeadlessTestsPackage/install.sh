#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
template_dir="${script_dir}/template"

target_project_path=""
test_project_name=""
test_project_directory=""
solution_path=""

usage() {
    cat <<'EOF'
Usage:
  ./install.sh --target <path-to-app.csproj> [--test-project-name <name>] [--test-dir <dir>] [--solution <path-to-sln-or-slnx>]
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --target)
            target_project_path="$2"
            shift 2
            ;;
        --test-project-name)
            test_project_name="$2"
            shift 2
            ;;
        --test-dir)
            test_project_directory="$2"
            shift 2
            ;;
        --solution)
            solution_path="$2"
            shift 2
            ;;
        --help|-h)
            usage
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
            usage >&2
            exit 1
            ;;
    esac
done

if [[ -z "${target_project_path}" ]]; then
    echo "--target is required." >&2
    usage >&2
    exit 1
fi

target_project_path="$(realpath "${target_project_path}")"
if [[ ! -f "${target_project_path}" ]]; then
    echo "Target project not found: ${target_project_path}" >&2
    exit 1
fi

target_project_directory="$(dirname "${target_project_path}")"
target_project_file_name="$(basename "${target_project_path}")"
target_project_base_name="${target_project_file_name%.csproj}"

if [[ -z "${test_project_name}" ]]; then
    test_project_name="${target_project_base_name}.HeadlessTests"
fi

find_solution_path() {
    find "${target_project_directory}" "$(dirname "${target_project_directory}")" -maxdepth 1 -type f \
        \( -name '*.sln' -o -name '*.slnx' \) | head -n 1 || true
}

if [[ -z "${solution_path}" ]]; then
    solution_path="$(find_solution_path)"
fi

workspace_root="${target_project_directory}"
if [[ -n "${solution_path}" && -f "${solution_path}" ]]; then
    workspace_root="$(dirname "${solution_path}")"
fi

if [[ -z "${test_project_directory}" ]]; then
    if [[ "${target_project_directory}" == "${workspace_root}" ]]; then
        test_project_directory="${workspace_root}/tests/${test_project_name}"
    else
        test_project_directory="$(dirname "${target_project_directory}")/${test_project_name}"
    fi
else
    test_project_directory="$(realpath -m "${test_project_directory}")"
fi

mkdir -p "${test_project_directory}"

extract_xml_value() {
    local xml_file="$1"
    local tag_name="$2"
    sed -n "s:.*<${tag_name}>\\([^<]*\\)</${tag_name}>.*:\\1:p" "${xml_file}" | head -n 1
}

target_framework="$(extract_xml_value "${target_project_path}" "TargetFramework")"
if [[ -z "${target_framework}" ]]; then
    target_frameworks="$(extract_xml_value "${target_project_path}" "TargetFrameworks")"
    target_framework="${target_frameworks%%;*}"
fi

if [[ -z "${target_framework}" ]]; then
    echo "Unable to determine TargetFramework from ${target_project_path}" >&2
    exit 1
fi

avalonia_version="$(sed -n 's:.*<PackageReference Include="Avalonia" Version="\([^"]*\)".*:\1:p' "${target_project_path}" | head -n 1)"
if [[ -z "${avalonia_version}" ]]; then
    avalonia_version="11.3.12"
fi

relative_target_project_path="$(realpath --relative-to="${test_project_directory}" "${target_project_path}")"
relative_test_project_directory="$(realpath --relative-to="${target_project_directory}" "${test_project_directory}")"
test_namespace="$(printf '%s' "${test_project_name}" | tr '.-' '__')"

ensure_target_project_excludes() {
    if [[ "${relative_test_project_directory}" == ..* ]]; then
        return
    fi

    local include_pattern="${relative_test_project_directory//\\//}/**"
    if grep -Fq "${include_pattern}" "${target_project_path}"; then
        return
    fi

    local temp_file
    temp_file="$(mktemp)"

    awk -v pattern="${include_pattern}" '
        /<\/Project>/ {
            print "  <ItemGroup>"
            print "    <Compile Remove=\"" pattern "\" />"
            print "    <EmbeddedResource Remove=\"" pattern "\" />"
            print "    <None Remove=\"" pattern "\" />"
            print "    <Content Remove=\"" pattern "\" />"
            print "    <Page Remove=\"" pattern "\" />"
            print "    <AvaloniaResource Remove=\"" pattern "\" />"
            print "  </ItemGroup>"
        }
        { print }
    ' "${target_project_path}" > "${temp_file}"

    mv "${temp_file}" "${target_project_path}"
    echo "Updated ${target_project_path} to exclude ${include_pattern}"
}

create_from_template() {
    local source_name="$1"
    local destination_name="$2"
    sed \
        -e "s|__TEST_PROJECT_NAME__|${test_project_name}|g" \
        -e "s|__TEST_NAMESPACE__|${test_namespace}|g" \
        -e "s|__TARGET_PROJECT_PATH__|${relative_target_project_path}|g" \
        -e "s|__TARGET_PROJECT_NAME__|${target_project_base_name}|g" \
        -e "s|__TARGET_FRAMEWORK__|${target_framework}|g" \
        -e "s|__AVALONIA_VERSION__|${avalonia_version}|g" \
        "${template_dir}/${source_name}" > "${test_project_directory}/${destination_name}"
}

create_from_template "HeadlessTests.csproj.template" "${test_project_name}.csproj"
create_from_template "TestAppBuilder.cs.template" "TestAppBuilder.cs"
create_from_template "TestApp.cs.template" "TestApp.cs"
create_from_template "TestOutputHelper.cs.template" "TestOutputHelper.cs"
create_from_template "HeadlessGifHarness.cs.template" "HeadlessGifHarness.cs"
create_from_template "RenderingSmokeTests.cs.template" "RenderingSmokeTests.cs"
create_from_template "gitignore.template" ".gitignore"

ensure_target_project_excludes

generated_project_path="${test_project_directory}/${test_project_name}.csproj"

if [[ -n "${solution_path}" && -f "${solution_path}" ]]; then
    dotnet sln "${solution_path}" add "${generated_project_path}" >/dev/null
    echo "Added ${generated_project_path} to ${solution_path}"
fi

echo "Created ${generated_project_path}"
echo "Run: dotnet test \"${generated_project_path}\""
