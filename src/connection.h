
#ifndef OWLTREE_CONNECTION_H
#define OWLTREE_CONNECTION_H

#include <string>
#include <stdint.h>

namespace owltree {

class connection {

    public:
        enum role {
            server,
            client
        };

        struct args {
            public:
                role role = server;
                std::string server_addr = "127.0.0.1";
                int port = 8080;
                uint8_t max_clients = 4;
                int buffer_size = 2048;

                bool threaded = false;
                int thread_update_delta = 40;
        };

    private:
        role _role;

        // network_buffer _buffer;

        // concurrent_queue _client_events;

    public:
        connection(args args);



};

}

#endif