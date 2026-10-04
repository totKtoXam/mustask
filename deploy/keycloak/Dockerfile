# syntax=docker/dockerfile:1
#
# The stand's Keycloak image with the provider `mustask-user-projection`
# (mod-014, ADR-0025 § 1). Mustask.AppHost builds it (AddDockerfile) instead of
# pulling the bare image; scripts/verify.sh builds the `provider` stage.
#
#   keycloak  the Keycloak image the stand runs (KEYCLOAK_IMAGE:KEYCLOAK_TAG)
#   provider  compiles and tests the provider against that very Keycloak's SPI
#             version (mvn verify: compile, unit tests, the read-only guard)
#   test      the same as `provider` — the name ADR-0025 gives the check
#   (final)   that Keycloak plus the provider's jar
#
# The tag default must equal Mustask.AppHost's `Keycloak:Tag` default
# (KeycloakProviderImageTests holds the two together). Maven's repository is a
# BuildKit cache mount: a change to the provider's sources rebuilds without the
# network; the first build on a machine, a tag change or a pom.xml change needs it.

ARG KEYCLOAK_IMAGE=quay.io/keycloak/keycloak
ARG KEYCLOAK_TAG=26.4

FROM ${KEYCLOAK_IMAGE}:${KEYCLOAK_TAG} AS keycloak

FROM maven:3.9-eclipse-temurin-21 AS provider
WORKDIR /build
# Only the SPI jar's name is needed: it carries the exact Keycloak version.
COPY --from=keycloak /opt/keycloak/lib/lib/main/org.keycloak.keycloak-server-spi-*.jar /keycloak-version/
COPY providers/user-projection/pom.xml ./pom.xml
COPY providers/user-projection/src ./src
RUN --mount=type=cache,target=/root/.m2 \
    KEYCLOAK_VERSION="$(ls /keycloak-version | sed -n 's/^org\.keycloak\.keycloak-server-spi-\([0-9][^/]*\)\.jar$/\1/p')" \
    && test -n "$KEYCLOAK_VERSION" \
    && echo "Building mustask-user-projection against Keycloak $KEYCLOAK_VERSION" \
    && mvn -B -ntp -Dkeycloak.version="$KEYCLOAK_VERSION" verify \
    && cp target/mustask-user-projection.jar /mustask-user-projection.jar

FROM provider AS test

FROM keycloak
COPY --from=provider --chown=1000:0 /mustask-user-projection.jar /opt/keycloak/providers/mustask-user-projection.jar
# start-dev keeps its database under data/h2. Created here, as Keycloak's own
# user, so the stand's named volume mounted on it starts with that ownership.
RUN mkdir -p /opt/keycloak/data/h2
