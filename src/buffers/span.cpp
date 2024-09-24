
#include "span.h"
#include <iostream>
#include <stdint.h>

int main() {
    uint8_t* arr = new uint8_t[15];
    owltree::buffer_span a = owltree::buffer_span(arr, 0, 10);

    uint16_t u16 = 10;
    uint32_t u32 = 840;
    uint64_t u64 = 5555555;

    int16_t s16 = 88;
    int32_t s32 = 90002;
    int64_t s64 = 120034588;
    
    float f = 16.38;
    double d = 9.00348;

    a.try_encode(u16);
    std::cout << std::to_string(a.decode_uint16()) << std::endl;
    a.try_encode(u32);
    std::cout << std::to_string(a.decode_uint32()) << std::endl;
    a.try_encode(u64);
    std::cout << std::to_string(a.decode_uint64()) << std::endl;

    a.try_encode(s16);
    std::cout << std::to_string(a.decode_int16()) << std::endl;
    a.try_encode(s32);
    std::cout << std::to_string(a.decode_int32()) << std::endl;
    a.try_encode(s64);
    std::cout << std::to_string(a.decode_int64()) << std::endl;

    a.try_encode(f);
    std::cout << std::to_string(a.decode_float()) << std::endl;

    a.try_encode(d);
    std::cout << std::to_string(a.decode_double()) << std::endl;

    return 0;
}