
dotnet_bin  = find_program('dotnet')

dotnet_options = ['--nologo', '-r=linux-x64']

if get_option('buildtype') == 'release'
  dotnet_options += [ '-c=Release' ]
  dotnet_target = 'Release'
else
  dotnet_options += [ '-c=Debug' ]
  dotnet_target = 'Debug'
endif

dotnet_publish = custom_target('dotnet_publish',
  command: [
    'env',
    'DOTNET_CLI_TELEMETRY_OPTOUT=1', # Disable dotnet telemetry
    dotnet_bin,
    'publish',
    dotnet_options,
    meson.project_source_root() / meson.project_name() + '.csproj',
    '&&', 'cp', meson.project_source_root() / 'bin' / dotnet_target / 'net9.0/linux-x64/publish' / meson.project_name(), 
    '@OUTPUT@',
  ],
  build_by_default: true,
  build_always_stale: true,
  output: meson.project_name(),
  # console: true,
  install: true,
  install_dir: get_option('bindir'),
)
