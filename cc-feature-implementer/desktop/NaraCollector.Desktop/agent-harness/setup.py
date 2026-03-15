from setuptools import find_namespace_packages, setup

setup(
    name="cli-anything-nara-collector-desktop",
    version="0.1.0",
    description="CLI-Anything harness for NaraCollector.Desktop",
    python_requires=">=3.10",
    packages=find_namespace_packages(include=["cli_anything.*"]),
    install_requires=["click>=8.1"],
    entry_points={
        "console_scripts": [
            "cli-anything-nara-collector-desktop=cli_anything.nara_collector_desktop.nara_collector_desktop_cli:cli",
        ]
    },
    include_package_data=True,
)
