project('Stocks',
	version: '0.4.0',
	meson_version: '>= 1.5.0',
	default_options: [ 'warning_level=2', 'werror=false', ],
)

app_id = 'fi.taimila.Stocks'

i18n = import('i18n')
gnome = import('gnome')

pkgdatadir = get_option('prefix') / get_option('datadir') / meson.project_name()
moduledir = pkgdatadir / meson.project_name()
localedir = get_option('prefix') / get_option('localedir')

subdir(meson.project_name())
subdir('data')
subdir('po')

gnome.post_install(
     glib_compile_schemas: true,
    gtk_update_icon_cache: true,
  update_desktop_database: true,
)